<#
.SYNOPSIS
  One version for the three packages (U-114) and its tag (U-115).
  Without -Tag: rewrites "version" and every com.unidad.* dependency value of the three package.json in place (key
  order, indent and line endings kept), then runs release-check -Check Versions. Does not commit.
  -Tag: on a clean checkout at origin/main whose three versions equal -Version, runs release-check -Check All, creates
  the annotated tag v<version> and pushes it. Never moves an existing tag.
  Exit 0 | 1 a check failed | 2 usage (bad version, dirty tree, not at origin/main, tag exists)
.EXAMPLE
  pwsh -File tools/release.ps1 -Version 2.0.0-pre.2
  pwsh -File tools/release.ps1 -Version 2.0.0-pre.2 -Tag
#>
param(
    [string]$Version,
    [switch]$Tag,
    [string]$Repo = (Split-Path -Parent $PSScriptRoot),
    [string]$MainRef = 'origin/main'
)
$ErrorActionPreference = 'Stop'
$Packages = 'com.unidad.core', 'com.unidad.modules', 'com.unidad.unity'
$Check = Join-Path $PSScriptRoot 'release-check.ps1'

function Usage($why) { Write-Host "[release] $why"; exit 2 }
function GitR { $out = & git -C $Repo @args 2>$null; if ($LASTEXITCODE -ne 0) { return $null }; $out }

$branch = GitR rev-parse --abbrev-ref HEAD
$pattern = if ($branch -eq 'release/0.x') { '^0\.1\.\d+$' } else { '^2\.\d+\.\d+(-pre\.[1-9]\d*)?$' }
if (-not $Version -or $Version -notmatch $pattern) { Usage "version '$Version' does not match $pattern" }
if (-not (GitR rev-parse --git-dir)) { Usage "not a git repo: $Repo" }

if (-not $Tag) {
    foreach ($p in $Packages) {
        $file = Join-Path $Repo "Packages/$p/package.json"
        if (-not (Test-Path -LiteralPath $file)) { Usage "Packages/$p/package.json missing" }
        $text = [IO.File]::ReadAllText($file)
        # the top-level "version" is the one at two-space indent; dependency keys are the only "com.unidad.*" keys
        $text = [regex]::Replace($text, '(?m)^(  "version"\s*:\s*)"[^"]*"', "`${1}`"$Version`"")
        $text = [regex]::Replace($text, '("com\.unidad\.[a-z]+"\s*:\s*)"[^"]*"', "`${1}`"$Version`"")
        [IO.File]::WriteAllText($file, $text)
        Write-Host "[release] Packages/$p/package.json -> $Version"
    }
    & pwsh -NoProfile -File $Check -Check Versions -Repo $Repo
    exit $(if ($LASTEXITCODE -eq 0) { 0 } else { 1 })
}

$name = "v$Version"
if (GitR rev-parse -q --verify "refs/tags/$name") { Usage "tag $name exists; tags never move (U-115)" }
$null = GitR fetch -q --tags origin
if ($LASTEXITCODE -ne 0) { Usage 'git fetch origin failed' }
if (GitR ls-remote --tags origin "refs/tags/$name") { Usage "tag $name exists on origin; tags never move (U-115)" }
if (GitR status --porcelain) { Usage 'the checkout has uncommitted changes' }
$head = GitR rev-parse HEAD
$main = GitR rev-parse "$MainRef^{commit}"
if (-not $main -or $head -ne $main) { Usage "HEAD $head is not $MainRef $main (tag only merged main)" }
foreach ($p in $Packages) {
    $v = (GitR show "HEAD:Packages/$p/package.json") -join "`n" | ConvertFrom-Json | Select-Object -ExpandProperty version
    if ($v -ne $Version) { Write-Host "[release] $p is $v, expected $Version"; exit 1 }
}
& pwsh -NoProfile -File $Check -Check All -Repo $Repo
if ($LASTEXITCODE -ne 0) { exit 1 }
$null = GitR tag -a $name -m "unidad $Version" $head
if ($LASTEXITCODE -ne 0) { Usage "git tag $name failed" }
$null = GitR push -q origin "refs/tags/$name"
if ($LASTEXITCODE -ne 0) { Write-Host "[release] push of $name failed; the local tag stays, push it with: git push origin $name"; exit 1 }
Write-Host "[release] tagged $name at $head"
exit 0
