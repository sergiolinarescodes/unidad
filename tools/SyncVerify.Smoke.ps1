# Smoke tests of tools/sync-verify.ps1: plain asserts on throwaway temp git repos. Exit 1 on the first failure.
$ErrorActionPreference = 'Stop'
$Script = Join-Path $PSScriptRoot 'sync-verify.ps1'
$Root = Join-Path ([IO.Path]::GetTempPath()) "syncverify-smoke-$PID"
$passed = 0
$Tag = 'v2.0.0-pre.2'

function Assert($cond, $name) {
    if (-not $cond) { Write-Host "FAIL $name"; Write-Host ($script:out -join "`n"); Remove-Item -Recurse -Force $Root -ErrorAction SilentlyContinue; exit 1 }
    Write-Host "ok   $name"; $script:passed++
}
function Verify([string[]]$a) { $script:out = @(& pwsh -NoProfile -File $Script @a 2>&1 | ForEach-Object { "$_" }); $LASTEXITCODE }
function Says([string]$text) { [bool]($script:out | Where-Object { $_.Contains($text) }) }
function G { & git @args 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { throw "git $args failed" } }
function Write-File($path, $text) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    [IO.File]::WriteAllText($path, $text)
}
function Write-Packages($r, $version, $unityVersion) {
    foreach ($p in 'core', 'modules', 'unity') {
        $v = if ($p -eq 'unity') { $unityVersion } else { $version }
        $deps = if ($p -eq 'core') { @{} } else { @{ 'com.unidad.core' = $version } }
        Write-File "$r/Packages/com.unidad.$p/package.json" (@{ name = "com.unidad.$p"; version = $v; dependencies = $deps } | ConvertTo-Json)
        Write-File "$r/Packages/com.unidad.$p/Runtime/A.cs" "namespace Unidad.$p { }`n"
    }
}
function Init($r) {
    G init -q -b main $r
    G -C $r config user.email smoke@example.com; G -C $r config user.name smoke; G -C $r config core.autocrlf false
}
# unidad: v0.1.0 with the kept trees, v2.0.0-pre.1, then the replay commit tagged v2.0.0-pre.2.
function New-Unidad([string]$name, [string]$unityVersion = '2.0.0-pre.2', [string]$added = '') {
    $r = Join-Path $Root $name; Init $r
    Write-File "$r/Tools~/a.txt" "tool`n"; Write-File "$r/Skills~/s.md" "skill`n"; Write-File "$r/Documentation~/model-pipeline/d.md" "doc`n"
    G -C $r add -A; G -C $r commit -q -m base; G -C $r tag v0.1.0
    Write-Packages $r '2.0.0-pre.1' '2.0.0-pre.1'
    Write-File "$r/docs/SOURCES.md" "## Upstream from games`n`n| Game PR | Paths | Why |`n|---|---|---|`n"
    G -C $r add -A; G -C $r commit -q -m pre1; G -C $r tag -a v2.0.0-pre.1 -m 'unidad 2.0.0-pre.1'
    Write-Packages $r '2.0.0-pre.2' $unityVersion
    [IO.File]::AppendAllText("$r/docs/SOURCES.md", "| 11#5 | Packages/com.unidad.core/Runtime/A.cs | fix (v2.0.0-pre.2) |`n")
    if ($added) { Write-File "$r/Packages/com.unidad.core/Runtime/B.cs" "$added`n" }
    G -C $r add -A; G -C $r commit -q -m pre2; G -C $r tag -a $Tag -m 'unidad 2.0.0-pre.2'
    $r
}
function Up([string]$repo, [string[]]$extra) { Verify (@('-Phase', 'Up', '-Tag', $Tag, '-Repo', $repo, '-MainRef', 'main') + $extra) }

try {
    New-Item -ItemType Directory -Force $Root | Out-Null
    Assert ((Verify @()) -eq 2) 'NoArgs_IsUsageError'

    $u = New-Unidad 'unidad'
    Assert ((Up $u @('-Replayed', '5')) -eq 0) 'Up_PassesOnTaggedRelease'
    Assert ((Up (New-Unidad 'mismatch' '2.0.0-pre.1') @('-Replayed', '5')) -eq 1 -and (Says 'U2')) 'Up_FailsOnVersionMismatch'
    Assert ((Up $u @('-Replayed', '5,7')) -eq 1 -and (Says '11#7') -and -not (Says '11#5')) 'Up_FailsOnMissingUpstreamRow'
    Assert ((Up (New-Unidad 'gamename' -added '// Cds.Battle') @('-Replayed', '5')) -eq 1 -and (Says 'U5')) 'Up_FailsOnGameName'

    # game: master carries pre.1 plus game code; branch carry = the tag's packages + #7's edit + the mirrored toolset.
    $g = Join-Path $Root 'game'; Init $g
    Write-Packages $g '2.0.0-pre.1' '2.0.0-pre.1'
    Write-File "$g/Assets/Game.cs" "class Game { }`n"
    Write-File "$g/tools/repo.json" '{ "gameTools": ["install-drafter.ps1"] }'
    G -C $g add -A; G -C $g commit -q -m game
    G -C $g checkout -q -b carry
    Write-Packages $g '2.0.0-pre.2' '2.0.0-pre.2'
    Write-File "$g/Packages/com.unidad.core/Runtime/A.cs" "namespace Unidad.core { class Fixed { } }`n"
    Write-File "$g/tools/unity-lane.ps1" "# mirrored`n"
    $sha = (& git -C $u rev-parse "$Tag^{commit}")
    $block = ('com.unidad.core', 'com.unidad.modules', 'com.unidad.unity' | ForEach-Object { "| $_ | $Tag | $sha |" }) -join "`n"
    $sources = "## Carried framework`n`n<!-- carry-unidad:begin -->`n| Package | Tag | unidad commit |`n|---|---|---|`n$block`n<!-- carry-unidad:end -->`n`n" +
        "## Divergences from the carried tag`n`n| PR | Path | Why |`n|---|---|---|`n| #7 | Packages/com.unidad.core/Runtime/A.cs | kept after the carry |`n"
    Write-File "$g/docs/SOURCES.md" $sources
    G -C $g add -A; G -C $g commit -q -m carry
    function Down([string]$at, [string[]]$extra) { Verify (@('-Phase', 'Down', '-Tag', $Tag, '-Repo', $u, '-Game', $g, '-Base', 'main', '-At', $at) + $extra) }

    Assert ((Down 'carry' @('-Replayed', '5')) -eq 0) 'Down_PassesOnCleanCarry'

    G -C $g checkout -q -b drift carry
    Write-File "$g/Packages/com.unidad.modules/Runtime/A.cs" "namespace Unidad.modules { class Drift { } }`n"
    G -C $g commit -q -am drift
    Assert ((Down 'drift' @()) -eq 1 -and (Says 'D2') -and (Says 'Packages/com.unidad.modules/Runtime/A.cs')) 'Down_FailsOnUncoveredDrift'

    G -C $g checkout -q -b leftover carry
    [IO.File]::AppendAllText("$g/docs/SOURCES.md", "| #5 | Packages/com.unidad.core/Runtime/A.cs | replayed |`n")
    G -C $g commit -q -am leftover
    Assert ((Down 'leftover' @('-Replayed', '5')) -eq 1 -and (Says '#5') -and (Says 'D3')) 'Down_FailsOnLeftoverReplayedRow'

    G -C $g checkout -q -b gamefile carry
    Write-File "$g/Assets/x.cs" "class X { }`n"; Write-File "$g/tools/install-drafter.ps1" "# game tool`n"
    G -C $g add -A; G -C $g commit -q -m gamefile
    Assert ((Down 'gamefile' @()) -eq 1 -and (Says 'D4') -and (Says 'Assets/x.cs') -and (Says 'tools/install-drafter.ps1')) 'Down_FailsOnGameFileTouched'

    $strict = Join-Path $Root 'strict.txt'; $loose = Join-Path $Root 'loose.txt'
    Write-File $strict "boot=ok`nstrict=true`nstubs.winning=3`nerrors=0`n"
    Write-File $loose "boot=ok`nstrict=false`nstubs.winning=3`nerrors=0`n"
    $strictFails = (Down 'carry' @('-Report', $strict, '-Strict')) -eq 1 -and (Says 'stubs.winning=3')
    Assert ($strictFails -and (Down 'carry' @('-Report', $loose)) -eq 0 -and (Says 'stubs.winning=3')) 'Boot_StrictNeedsZeroStubs'
}
finally { Remove-Item -Recurse -Force $Root -ErrorAction SilentlyContinue }
Write-Host "$passed passed"
exit 0
