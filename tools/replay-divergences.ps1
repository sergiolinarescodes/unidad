<#
.SYNOPSIS
  The up half of a sync (U-54): the game's framework edits since the carried tag, as a patch for unidad.
  Diffs the game's Packages/com.unidad.* at -Since (the sync start S) against -CarriedTag and keeps the files covered by
  a Divergences row of the game's docs/SOURCES.md at S (rows present at S are rows whose PR merged before it).
  Refuses (exit 2) a changed file no row covers, a changed analyzer DLL, and a package.json version/dependency edit
  (U-116). Prints "rows: #a,#b" (replayed) and "stale: #x" (rows whose paths no longer differ; the carry drops both).
  Game-agnostic: every repo path is a parameter. Paths are identical in both repos, so the patch applies here as is:
    git apply --3way --index <Out>
  Exit 0 | 1 usage/setup | 2 refused
.EXAMPLE
  pwsh -File tools/replay-divergences.ps1 -GameRepo ../11-unidad-unity -CarriedTag v2.0.0-pre.1 -Since <S> -Out $env:TEMP/replay.patch
#>
param(
    [string]$GameRepo,
    [string]$CarriedTag,
    [string]$Since,
    [string]$Out,
    [string]$Source = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
$Frameworks = 'Packages/com.unidad.core', 'Packages/com.unidad.modules', 'Packages/com.unidad.unity'
$AnalyzerDll = 'Packages/com.unidad.core/Analyzers/Unidad.Analyzers.dll'

function Usage($why) { Write-Host "[replay] $why"; exit 1 }
function Refuse($why) { Write-Host "[replay] refused: $why"; $script:refused++ }
function GitIn($dir) { $out = & git -c core.quotepath=off -C $dir @args 2>$null; if ($LASTEXITCODE -ne 0) { return $null }; $out }

if (-not $GameRepo -or -not $CarriedTag -or -not $Since) { Usage 'usage: -GameRepo <game checkout> -CarriedTag <tag> -Since <game sha> [-Out <patch>]' }
if (-not (GitIn $GameRepo rev-parse --git-dir)) { Usage "not a git repo: $GameRepo" }
$S = GitIn $GameRepo rev-parse --verify -q "$Since^{commit}"
if (-not $S) { Usage "unknown commit $Since in $GameRepo" }
$ref = "refs/unidad/$CarriedTag"
$null = GitIn $GameRepo fetch -q $Source "+refs/tags/${CarriedTag}:$ref"
if (-not (GitIn $GameRepo rev-parse --verify -q "$ref^{commit}")) { Usage "tag $CarriedTag not fetchable from $Source" }
if (-not $Out) { $Out = Join-Path ([IO.Path]::GetTempPath()) "replay-$CarriedTag.patch" }

# Divergences rows at S: PR number + path prefixes (a cell may list several, comma separated, in backticks or not).
$rows = @()
$in = $false
foreach ($l in @(GitIn $GameRepo show "${S}:docs/SOURCES.md")) {
    if ($l -match '^#+\s') { $in = $l -match 'Divergences'; continue }
    if (-not $in -or $l -notmatch '^\|\s*#(\d+)\s*\|([^|]*)\|') { continue }
    $paths = @($Matches[2] -split ',' | ForEach-Object { $_.Trim().Trim('`').Trim().TrimEnd('*') } | Where-Object { $_ })
    $rows += [pscustomobject]@{ Pr = [int]$Matches[1]; Paths = $paths; Hit = $false }
}

$script:refused = 0
$changed = @(GitIn $GameRepo diff --name-only $ref $S -- @Frameworks)
$keep = @()
foreach ($f in $changed) {
    if ($f -eq $AnalyzerDll) { Refuse "$f changed: analyzer fixes are unidad PRs (U-116)"; continue }
    if ($f -like '*/package.json') {
        $edits = @(GitIn $GameRepo diff --unified=0 $ref $S -- $f | Where-Object { $_ -match '^[+-][^+-]' -and $_ -match '"version"\s*:|"com\.[^"]+"\s*:' })
        if ($edits) { Refuse "$f edits a version or dependency field (U-116)"; continue }
    }
    $hits = @($rows | Where-Object { $r = $_; $r.Paths | Where-Object { $f.StartsWith($_) } })
    if (-not $hits) { Refuse "$f differs from $CarriedTag with no Divergences row at $S"; continue }
    $hits | ForEach-Object { $_.Hit = $true }
    $keep += $f
}
foreach ($r in $rows | Where-Object { $_.Paths -contains $AnalyzerDll }) { Refuse "row #$($r.Pr) names the analyzer DLL (U-116)" }
if ($script:refused) { Write-Host "[replay] $script:refused refused; add the rows in the game or revert the drift there, then run again"; exit 2 }

$Out = [IO.Path]::GetFullPath($Out)
if ($keep) {
    # git writes the file itself: piping through PowerShell would strip the CRs of CRLF lines
    $null = GitIn $GameRepo diff --binary "--output=$Out" $ref $S -- @keep
    if ($LASTEXITCODE -ne 0) { Usage "git diff into $Out failed" }
} else { [IO.File]::WriteAllText($Out, '') }
Write-Host "[replay] $($keep.Count) files -> $Out"
Write-Host "rows: $((@($rows | Where-Object Hit | ForEach-Object { "#$($_.Pr)" }) | Select-Object -Unique) -join ',')"
$stale = @($rows | Where-Object { -not $_.Hit } | ForEach-Object { "#$($_.Pr)" }) | Select-Object -Unique
if ($stale) { Write-Host "stale: $($stale -join ',')" }
exit 0
