# Smoke tests of release.ps1, sandbox-gate.ps1, replay-divergences.ps1 and carry-unidad.ps1: plain asserts on throwaway
# temp git repos. Exit 1 on the first failure.
$ErrorActionPreference = 'Stop'
$Tools = $PSScriptRoot
$Root = Join-Path ([IO.Path]::GetTempPath()) "release-tooling-smoke-$PID"
$passed = 0

function Assert($cond, $name) {
    if (-not $cond) { Write-Host "FAIL $name"; Remove-Item -Recurse -Force $Root -ErrorAction SilentlyContinue; exit 1 }
    Write-Host "ok   $name"; $script:passed++
}
function Run([string]$script, [string[]]$a) {
    $script:out = @(& pwsh -NoProfile -File (Join-Path $Tools $script) @a 2>&1 | ForEach-Object { "$_" })
    $LASTEXITCODE
}
function G { & git @args 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { throw "git $args failed" } }
function Put($path, $text) { New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null; [IO.File]::WriteAllText($path, $text) }
function New-Repo([string]$name) {
    $r = Join-Path $Root $name
    G init -q -b main $r
    G -C $r config user.email smoke@example.com; G -C $r config user.name smoke; G -C $r config core.autocrlf false
    $r
}
function Commit($r, $msg) { G -C $r add -A; G -C $r commit -q -m $msg; (& git -C $r rev-parse HEAD) }
function Package($name, $version, $deps) {
    $d = ($deps.Keys | ForEach-Object { "    `"$_`": `"$($deps[$_])`"" }) -join ",`n"
    "{`n  `"name`": `"$name`",`n  `"version`": `"$version`",`n  `"unity`": `"7000.0`",`n  `"dependencies`": {`n$d`n  }`n}`n"
}
function Sources([string[]]$rows) {
    "# Sources`n`n## Divergences from the carried tag`n`n| PR | Path | Why |`n|---|---|---|`n" + (($rows | ForEach-Object { "$_`n" }) -join '')
}

try {
    New-Item -ItemType Directory -Force $Root | Out-Null
    $env:UNIDAD_TOOLS_HOME = Join-Path $Root 'home'   # the editor lock of these tests is not the machine's

    # --- release.ps1 ---
    $r = New-Repo 'release'
    Put "$r/Packages/com.unidad.core/package.json" (Package 'com.unidad.core' '2.0.0-pre.1' ([ordered]@{}))
    Put "$r/Packages/com.unidad.modules/package.json" (Package 'com.unidad.modules' '2.0.0-pre.1' ([ordered]@{ 'com.unidad.core' = '2.0.0-pre.1' }))
    Put "$r/Packages/com.unidad.unity/package.json" (Package 'com.unidad.unity' '2.0.0-pre.1' ([ordered]@{ 'com.unidad.core' = '2.0.0-pre.1'; 'com.unidad.modules' = '2.0.0-pre.1'; 'com.unity.inputsystem' = '6.7.0' }))
    $null = Commit $r 'packages'
    G -C $r tag -a v2.0.0-pre.1 -m pre1
    $tagged = & git -C $r rev-parse v2.0.0-pre.1
    $code = Run 'release.ps1' @('-Version', '2.0.0-pre.2', '-Repo', $r)
    $unity = Get-Content -Raw "$r/Packages/com.unidad.unity/package.json"
    $json = $unity | ConvertFrom-Json
    $changed = @(git -C $r status --porcelain)
    Assert ($code -eq 0 -and $changed.Count -eq 3 -and $json.version -eq '2.0.0-pre.2' -and $json.dependencies.'com.unidad.core' -eq '2.0.0-pre.2' -and
        $json.dependencies.'com.unidad.modules' -eq '2.0.0-pre.2' -and $json.dependencies.'com.unity.inputsystem' -eq '6.7.0' -and
        $unity -eq (Package 'com.unidad.unity' '2.0.0-pre.2' ([ordered]@{ 'com.unidad.core' = '2.0.0-pre.2'; 'com.unidad.modules' = '2.0.0-pre.2'; 'com.unity.inputsystem' = '6.7.0' }))) 'Release_BumpTouchesThreeFilesInPlace'
    Assert ((Run 'release.ps1' @('-Version', '2.0', '-Repo', $r)) -eq 2 -and (Run 'release.ps1' @('-Version', '2.0.0-pre.0', '-Repo', $r)) -eq 2) 'Release_RefusesBadVersion'
    G -C $r checkout -q -- .
    $code = Run 'release.ps1' @('-Version', '2.0.0-pre.1', '-Tag', '-Repo', $r)
    Assert ($code -eq 2 -and (& git -C $r rev-parse v2.0.0-pre.1) -eq $tagged) 'Release_TagRefusesToMoveATag'

    # --- sandbox-gate.ps1 on a fake lane ---
    $lane = Join-Path $Root 'fake-lane.ps1'
    Put $lane "if (`$env:FAKE_LANE_OUT) { Write-Host `$env:FAKE_LANE_OUT }; exit [int]`$env:FAKE_LANE_EXIT`n"
    function Gate([int]$exit, [string]$text) {
        $env:FAKE_LANE_EXIT = $exit; $env:FAKE_LANE_OUT = $text
        try { Run 'sandbox-gate.ps1' @('-Lane', $lane, '-SkipDotnet', '-Branch', 'main') } finally { Remove-Item Env:FAKE_LANE_EXIT, Env:FAKE_LANE_OUT -ErrorAction SilentlyContinue }
    }
    Assert ((Gate 0 'lane: green') -eq 0 -and (Gate 1 'lane: Bee red') -eq 1 -and
        (Gate 0 'Assets/A.cs(3,9): error CS0103: The name x does not exist') -eq 1 -and (Gate 3 'lane: waited') -eq 3 -and
        (Run 'sandbox-gate.ps1' @('-WhatIf', '-Branch', 'main')) -eq 0) 'Gate_MapsLaneExitCodesAndErrorLines'

    # --- replay-divergences.ps1 ---
    $u = New-Repo 'unidad'
    Put "$u/Packages/com.unidad.core/A.cs" "class A { }`n"
    Put "$u/Packages/com.unidad.core/B.cs" "class B { }`n"
    Put "$u/Packages/com.unidad.core/C.cs" "class C { }`n"
    Put "$u/Packages/com.unidad.modules/M.cs" "class M { }`n"
    Put "$u/Packages/com.unidad.unity/U.cs" "class U { }`n"
    Put "$u/tools/lane.ps1" "# v1`n"
    Put "$u/tools/repo.json" '{ "name": "unidad" }'
    Put "$u/tools/meta-roots.txt" "Packages`n"
    Put "$u/tools/mine.ps1" "# unidad's`n"
    $null = Commit $u 'framework'
    G -C $u tag -a T -m T

    $g = New-Repo 'game'
    Copy-Item -Recurse "$u/Packages" "$g/Packages"
    Put "$g/tools/lane.ps1" "# v1`n"
    Put "$g/tools/old.ps1" "# gone upstream`n"
    Put "$g/tools/mine.ps1" "# the game's`n"
    Put "$g/tools/repo.json" '{ "name": "game", "gameTools": ["mine.ps1"] }'
    Put "$g/tools/meta-roots.txt" "Assets`n"
    Put "$g/docs/SOURCES.md" (Sources @('| #5 | Packages/com.unidad.core/A.cs | fix A |', '| #7 | Packages/com.unidad.unity/U.cs | not edited yet |'))
    $null = Commit $g 'carry T'
    Put "$g/Packages/com.unidad.core/A.cs" "class A { int x; }`n"
    $S = Commit $g 'game PR #5'
    $patch = Join-Path $Root 'replay.patch'
    $code = Run 'replay-divergences.ps1' @('-GameRepo', $g, '-CarriedTag', 'T', '-Since', $S, '-Out', $patch, '-Source', $u)
    & git -C $u apply --check $patch 2>&1 | Out-Null
    Assert ($code -eq 0 -and $LASTEXITCODE -eq 0 -and ($out -contains 'rows: #5') -and ($out -contains 'stale: #7')) 'Replay_KeepsCoveredHunksAndAppliesUpstream'

    Put "$g/Packages/com.unidad.core/B.cs" "class B { int y; }`n"
    $S2 = Commit $g 'uncovered drift'
    $code = Run 'replay-divergences.ps1' @('-GameRepo', $g, '-CarriedTag', 'T', '-Since', $S2, '-Out', $patch, '-Source', $u)
    Assert ($code -eq 2 -and ($out -match 'B\.cs')) 'Replay_RefusesUncoveredDrift'
    G -C $g reset -q --hard $S

    # --- carry-unidad.ps1 ---
    Put "$u/Packages/com.unidad.core/A.cs" "class A { int x; int z; }`n"
    Put "$u/tools/lane.ps1" "# v2`n"
    $env:GIT_COMMITTER_DATE = '2001-01-01T00:00:00Z'; $env:GIT_AUTHOR_DATE = '2001-01-01T00:00:00Z'
    try { $null = Commit $u 'replayed #5' } finally { Remove-Item Env:GIT_COMMITTER_DATE, Env:GIT_AUTHOR_DATE }
    G -C $u tag -a T2 -m T2
    $t2 = & git -C $u rev-parse 'T2^{commit}'

    Put "$g/Packages/com.unidad.core/A.cs" "dirty`n"
    $code = Run 'carry-unidad.ps1' @('-Tag', 'T2', '-Source', $u, '-Game', $g)
    Assert ($code -eq 2 -and (Get-Content -Raw "$g/Packages/com.unidad.core/A.cs") -eq "dirty`n" -and (Get-Content -Raw "$g/tools/lane.ps1") -eq "# v1`n") 'Carry_RefusesDirtyTree'
    G -C $g checkout -q -- .

    $code = Run 'carry-unidad.ps1' @('-Tag', 'T2', '-Source', $u, '-Game', $g, '-Replayed', '5')
    $src = Get-Content -Raw "$g/docs/SOURCES.md"
    Assert ($code -eq 0 -and (Get-Content -Raw "$g/Packages/com.unidad.core/A.cs") -eq "class A { int x; int z; }`n" -and $src -match "com\.unidad\.core \| T2 \| $t2") 'Carry_ReplacesFoldersAndWritesTheBlock'
    Assert ($src -notmatch '\| #5 \|' -and $src -match '\| #7 \| Packages/com\.unidad\.unity/U\.cs') 'Carry_RemovesOnlyTheReplayedRows'
    Assert ((Get-Content -Raw "$g/tools/lane.ps1") -eq "# v2`n" -and -not (Test-Path "$g/tools/old.ps1") -and
        (Get-Content -Raw "$g/tools/mine.ps1") -eq "# the game's`n" -and (Get-Content -Raw "$g/tools/repo.json") -match '"game"' -and
        (Get-Content -Raw "$g/tools/meta-roots.txt") -eq "Assets`n") 'Carry_MirrorsToolsButKeepsTheGamesData'
    Assert ((Get-Item "$g/Packages/com.unidad.unity/U.cs").LastWriteTime.Year -gt 2001) 'Carry_ResetsTimestamps'
    $block = @(($src -split "`n") | Select-String -Pattern '^## Carried framework$' -Context 0, 8 | ForEach-Object { $_.Context.PostContext })
    Assert ($block[0] -eq '' -and $block[1] -eq '<!-- carry-unidad:begin -->' -and $block[2] -eq '| Package | Tag | unidad commit |' -and $block[3] -eq '|---|---|---|' -and
        $block[4] -eq "| com.unidad.core | T2 | $t2 |" -and $block[5] -eq "| com.unidad.modules | T2 | $t2 |" -and
        $block[6] -eq "| com.unidad.unity | T2 | $t2 |" -and $block[7] -eq '<!-- carry-unidad:end -->') 'Carry_SourcesBlockFormat'

    $S3 = Commit $g 'carry T2'
    Put "$g/Packages/com.unidad.core/C.cs" "class C { int later; }`n"
    $null = Commit $g 'game PR #9 after the sync started'
    $code = Run 'carry-unidad.ps1' @('-Tag', 'T2', '-Source', $u, '-Game', $g, '-Since', $S3)
    $src = Get-Content -Raw "$g/docs/SOURCES.md"
    Assert ($code -eq 0 -and (Get-Content -Raw "$g/Packages/com.unidad.core/C.cs") -eq "class C { int later; }`n" -and
        ([regex]::Matches($src, 'carry-unidad:begin')).Count -eq 1) 'Carry_SinceReappliesALaterEdit'
}
finally {
    Remove-Item Env:UNIDAD_TOOLS_HOME -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $Root -ErrorAction SilentlyContinue
}
Write-Host "$passed passed"
exit 0
