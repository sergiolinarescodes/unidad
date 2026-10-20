# Smoke tests of tools/release-check.ps1: plain asserts on throwaway temp git repos. Exit 1 on the first failure.
$ErrorActionPreference = 'Stop'
$Script = Join-Path $PSScriptRoot 'release-check.ps1'
$Root = Join-Path ([IO.Path]::GetTempPath()) "release-smoke-$PID"
$passed = 0

function Assert($cond, $name) {
    if (-not $cond) { Write-Host "FAIL $name"; Remove-Item -Recurse -Force $Root -ErrorAction SilentlyContinue; exit 1 }
    Write-Host "ok   $name"; $script:passed++
}
function Check([string]$repo, [string[]]$extra) {
    $null = & pwsh -NoProfile -File $Script -Repo $repo @extra 2>&1
    $LASTEXITCODE
}
function G { & git @args 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { throw "git $args failed" } }
function Write-File($path, $text) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    [IO.File]::WriteAllText($path, $text)
}
function New-Repo([string]$name) {
    $r = Join-Path $Root $name
    G init -q -b main $r
    G -C $r config user.email smoke@example.com; G -C $r config user.name smoke; G -C $r config core.autocrlf false
    Write-File "$r/Tools~/a.txt" "tool`n"
    Write-File "$r/Skills~/s.md" "skill`n"
    Write-File "$r/Documentation~/model-pipeline/d.md" "doc`n"
    Write-File "$r/old.txt" "old`n"
    G -C $r add -A; G -C $r commit -q -m base; G -C $r tag v0.1.0
    G -C $r rm -q old.txt; G -C $r commit -q -m cut
    $r
}
function Write-Package($repo, $name, $version, $deps) {
    Write-File "$repo/Packages/$name/package.json" (@{ name = $name; version = $version; dependencies = $deps } | ConvertTo-Json)
}

try {
    New-Item -ItemType Directory -Force $Root | Out-Null
    $kept = '-KeptTreesFrom', 'v0.1.0'

    $r = New-Repo 'kept'
    Assert ((Check $r (@('-Check', 'KeptTrees') + $kept)) -eq 0) 'KeptTrees_PassesWhenUntouched'

    [IO.File]::AppendAllText("$r/Tools~/a.txt", 'x'); G -C $r commit -q -am byte
    Assert ((Check $r (@('-Check', 'KeptTrees') + $kept)) -eq 1) 'KeptTrees_FailsOnOneByteChange'

    $r = New-Repo 'pkg'
    Write-File "$r/package.json" '{}'; G -C $r add package.json; G -C $r commit -q -m pkg
    Assert ((Check $r @('-Check', 'NoRootPackage')) -eq 1) 'NoRootPackage_FailsWhenPresent'

    $r = New-Repo 'branch'
    $bare = Join-Path $Root 'origin.git'; G init -q --bare $bare; G -C $r remote add origin $bare; G -C $r push -q origin main
    Assert ((Check $r @('-Check', 'ReleaseBranch')) -eq 1) 'ReleaseBranch_FailsWhenMissing'

    $r = New-Repo 'versions'
    Assert ((Check $r @('-Check', 'Versions')) -eq 0) 'Versions_SkippedWithoutPackages'
    Write-Package $r 'com.unidad.core' '2.0.0-pre.1' @{}
    Write-Package $r 'com.unidad.modules' '2.0.0-pre.1' @{ 'com.unidad.core' = '2.0.0-pre.1' }
    Write-Package $r 'com.unidad.unity' '2.0.0-pre.1' @{ 'com.unidad.core' = '2.0.0-pre.1'; 'com.unidad.modules' = '2.0.0-pre.1'; 'com.unity.inputsystem' = '6.7.0' }
    Assert ((Check $r @('-Check', 'Versions')) -eq 0) 'Versions_PassesOnEqualVersions'
    Write-Package $r 'com.unidad.unity' '2.0.0-pre.2' @{ 'com.unidad.core' = '2.0.0-pre.1' }
    Assert ((Check $r @('-Check', 'Versions')) -eq 1) 'Versions_FailsOnMismatch'

    $good = Join-Path $Root 'good.json'; $bad = Join-Path $Root 'bad.json'
    Write-File $good '{ "dependencies": { "com.unidad.core": "https://github.com/sergiolinarescodes/unidad.git#v0.1.0" } }'
    Write-File $bad '{ "dependencies": { "com.unidad.core": "https://github.com/sergiolinarescodes/unidad.git" } }'
    Assert ((Check $r @('-Check', 'TemplatePinned', '-ManifestFile', $good)) -eq 0) 'TemplatePinned_PassesOnPinnedUrl'
    Assert ((Check $r @('-Check', 'TemplatePinned', '-ManifestFile', $bad)) -eq 1) 'TemplatePinned_FailsOnUntaggedUrl'

    $r = New-Repo 'all'
    $bare = Join-Path $Root 'all.git'; G init -q --bare $bare; G -C $r remote add origin $bare
    G -C $r push -q origin 'v0.1.0^{commit}:refs/heads/release/0.x'
    $sha = (& git -C $r rev-parse 'v0.1.0^{commit}')
    $allArgs = @('-Check', 'All', '-ManifestFile', $good, '-ReleaseSha', $sha) + $kept
    Assert ((Check $r $allArgs) -eq 0) 'All_PassesWhenEveryCheckPasses'
    Assert ((Check $r @('-Check', 'All', '-ManifestFile', $bad, '-ReleaseSha', $sha) + $kept) -eq 1) 'All_FailsWhenOneCheckFails'
    Assert ((Check $r @('-Check', 'Nope')) -eq 2) 'UnknownCheck_IsUsageError'
}
finally { Remove-Item -Recurse -Force $Root -ErrorAction SilentlyContinue }
Write-Host "$passed passed"
exit 0
