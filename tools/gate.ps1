# The developer gate and the loop gate, from tools/repo.json:
#   1. every `gates` entry in order ({dotnet} = the editor's bundled dotnet.exe); one with `when` runs only if a touched
#      path matches it, one with `requires` only if that path exists;
#   2. (not with -BuildOnly) `dotnet test` of each smokeMap project a touched path maps to, when it exists;
#   3. (not with -BuildOnly) `dotnet build` of each checkMap project, when a refpack exists (else "lane compile required").
#   pwsh -File tools/gate.ps1 [-Base origin/<main>] [-BuildOnly]       exit 0 green, 1 a step red (named), 2 environment
# Exit codes only: the console may be localised.
param([string]$Base, [switch]$BuildOnly, [string]$Root)
. (Join-Path $PSScriptRoot 'lib/UnidadTools.ps1')
if (!$Root) { $Root = Get-RepoRoot $PSScriptRoot }
$cfg = Get-RepoConfig $Root
if (!$Base) { $Base = "origin/$($cfg.mainBranch)" }
try {
    $dotnet = Resolve-UnityDotnet (Resolve-UnityEditor (Get-UnityProjectPath $cfg $Root) $null $cfg.editorVersion)
} catch { Write-Host "gate: $($_.Exception.Message)"; exit 2 }
$touched = Get-TouchedPaths $Base $Root

function Step([string]$Name, [string]$Run) {
    Write-Host "gate: $Name"
    $code = Invoke-RepoCommand $Run $Root $dotnet
    if ($code -ne 0) { Write-Host "gate: RED at '$Name' (exit $code)"; exit 1 }
}

foreach ($g in $cfg.gates) {
    if ($g.when -and ($BuildOnly -or !(Test-MatchesAny $touched @($g.when)))) { continue }
    if ($g.requires -and !(Test-Path -LiteralPath (Join-Path $Root $g.requires))) { Write-Host "gate: skipped '$($g.run)' ($($g.requires) does not exist yet)"; continue }
    Step $g.run $g.run
}
if ($BuildOnly) { Write-Host 'gate: green'; exit 0 }

foreach ($proj in Get-MappedProjects $touched $cfg.smokeMap) {
    if (!(Test-Path -LiteralPath (Join-Path $Root $proj))) { continue }
    Step "smoke $proj" "{dotnet} test $proj -v:q --nologo"
}
$checks = @(Get-MappedProjects $touched $cfg.checkMap | Where-Object { Test-Path -LiteralPath (Join-Path $Root $_) })
if ($checks) {
    if (Get-RefpackPointer $cfg.name) { foreach ($proj in $checks) { Step "check $proj" "{dotnet} build $proj -v:q -nologo" } }
    else { foreach ($proj in $checks) { Write-Host "gate: lane compile required for $proj (no refpack yet)" } }
}
Write-Host 'gate: green'
exit 0
