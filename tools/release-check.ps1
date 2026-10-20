<#
.SYNOPSIS
  Release invariants of unidad 2.x. Exit 0 all pass, 1 a check failed, 2 usage/setup error.
.EXAMPLE
  pwsh -File tools/release-check.ps1 -Check All
  pwsh -File tools/release-check.ps1 -Check TemplatePinned,ReleaseBranch
#>
param(
    [string[]]$Check = @('All'),
    [string]$Repo = (Split-Path -Parent $PSScriptRoot),
    # Test seams: compare kept trees against a ref instead of the pinned hashes, read the template manifest from a file
    # instead of the GitHub API, and expect another release/0.x commit.
    [string]$KeptTreesFrom,
    [string]$ManifestFile,
    [string]$ReleaseSha = 'ad1524f51c28d56c2dde813db6196b1ed205ab28'
)
$ErrorActionPreference = 'Stop'

$KeptTrees = [ordered]@{
    'Tools~'                        = '63fb6dcb5c2f678e05be3ac28b264e9b1cd1512e'
    'Skills~'                       = 'c9c0db2f6efc6ba33a76bc760f8b745bc618b397'
    'Documentation~/model-pipeline' = '4dc586e20563b47978b6b5c0c57813f9da2ae006'
}
$PinnedUrl = 'https://github.com/sergiolinarescodes/unidad.git#v0.1.0'
$TemplateRepo = 'sergiolinarescodes/unidad-template'
$VersionRegex = '^2\.\d+\.\d+(-pre\.[1-9]\d*)?$'
$Packages = 'com.unidad.core', 'com.unidad.modules', 'com.unidad.unity'
$AllChecks = 'KeptTrees', 'NoRootPackage', 'ReleaseBranch', 'TemplatePinned', 'Versions'

function GitOut { $out = & git -C $Repo @args 2>$null; if ($LASTEXITCODE -ne 0) { return $null }; $out }

function Test-KeptTrees {
    $why = @()
    foreach ($p in $KeptTrees.Keys) {
        $expected = if ($KeptTreesFrom) { GitOut rev-parse "${KeptTreesFrom}:$p" } else { $KeptTrees[$p] }
        $actual = GitOut rev-parse "HEAD:$p"
        if (-not $expected) { $why += "$p missing at $KeptTreesFrom" }
        elseif ($actual -ne $expected) { $why += "$p tree is $(if ($actual) { $actual } else { 'missing' }), expected $expected" }
    }
    $why
}

function Test-NoRootPackage {
    & git -C $Repo cat-file -e 'HEAD:package.json' 2>$null
    if ($LASTEXITCODE -eq 0) { 'HEAD has a root package.json' }
}

function Test-ReleaseBranch {
    $line = GitOut ls-remote origin refs/heads/release/0.x
    if (-not $line) { return 'origin has no release/0.x' }
    $sha = ($line -split '\s+')[0]
    if ($sha -ne $ReleaseSha) { "origin/release/0.x is $sha, expected $ReleaseSha" }
}

function Test-TemplatePinned {
    $json = if ($ManifestFile) { Get-Content -Raw $ManifestFile } else {
        $b64 = & gh api "repos/$TemplateRepo/contents/Packages/manifest.json" --jq .content 2>$null
        if ($LASTEXITCODE -ne 0) { return "cannot read $TemplateRepo Packages/manifest.json (gh api)" }
        [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($b64 -join '')))
    }
    try { $value = (ConvertFrom-Json $json).dependencies.'com.unidad.core' } catch { return "manifest is not JSON: $_" }
    if ($value -ne $PinnedUrl) { "com.unidad.core is '$value', expected '$PinnedUrl'" }
}

function Test-Versions {
    if (-not (Test-Path (Join-Path $Repo 'Packages'))) {
        Write-Host '[release-check] Versions: skipped (no Packages/ yet)'
        return
    }
    $why = @(); $found = @{}
    foreach ($name in $Packages) {
        $file = Join-Path $Repo "Packages/$name/package.json"
        if (-not (Test-Path $file)) { $why += "Packages/$name/package.json missing"; continue }
        try { $found[$name] = Get-Content -Raw $file | ConvertFrom-Json } catch { $why += "$name package.json is not JSON" }
    }
    if ($why) { return $why }
    $versions = $Packages | ForEach-Object { $found[$_].version } | Select-Object -Unique
    if (@($versions).Count -ne 1) { return "versions differ: $(($Packages | ForEach-Object { "$_=$($found[$_].version)" }) -join ', ')" }
    $v = [string]$versions
    if ($v -notmatch $VersionRegex) { $why += "version '$v' does not match $VersionRegex" }
    foreach ($name in $Packages) {
        $deps = $found[$name].dependencies
        if (-not $deps) { continue }
        foreach ($d in $deps.PSObject.Properties | Where-Object Name -like 'com.unidad.*') {
            if ($d.Value -ne $v) { $why += "$name depends on $($d.Name) $($d.Value), expected $v" }
        }
    }
    $why
}

$requested = @($Check | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($requested -contains 'All') { $requested = $AllChecks }
$unknown = $requested | Where-Object { $_ -notin $AllChecks }
if ($unknown) { Write-Host "[release-check] unknown check: $($unknown -join ', ') (expected All or $($AllChecks -join '|'))"; exit 2 }
if (-not (GitOut rev-parse --git-dir)) { Write-Host "[release-check] not a git repo: $Repo"; exit 2 }

$failed = 0
foreach ($c in $requested) {
    $why = @(& "Test-$c")
    if ($why) { $failed++; $why | ForEach-Object { Write-Host "[release-check] ${c}: $_" } }
    else { Write-Host "[release-check] ${c}: ok" }
}
exit ([int]($failed -gt 0))
