# What may be committed (U-63): binaries only on repo.json binaryAllow, no Library/, no forbidden pattern
# (tree.forbidden: shader_feature, game names under Packages/), and with -Pr a framework edit needs its Divergences row.
#   pwsh -File tools/check-tree.ps1 [-Pr <n>] [-Base origin/<main>]      exit 0 ok, 1 refused, 2 usage
# A PR whose tree.sourcesFile diff changes a carry-unidad line is exempt from the row rule (sync-verify -Phase Down
# checks it instead).
param([int]$Pr = 0, [string]$Base, [string]$Root)
. (Join-Path $PSScriptRoot 'lib/UnidadTools.ps1')
if (!$Root) { $Root = Get-RepoRoot $PSScriptRoot }
$cfg = Get-RepoConfig $Root
if (!$Base) { $Base = "origin/$($cfg.mainBranch)" }

$files = @(git -C $Root -c core.quotepath=off ls-files | ForEach-Object { $_ -replace '\\', '/' })
$prPaths = @(); $sourcesDiff = ''
if ($Pr -gt 0) {
    $prPaths = Get-TouchedPaths $Base $Root
    $mb = git -C $Root merge-base HEAD $Base
    $sourcesDiff = (git -C $Root diff $mb.Trim() HEAD -- $cfg.tree.sourcesFile) -join "`n"
}
$read = { param($f) $full = Join-Path $Root $f; if (Test-Path -LiteralPath $full) { Get-Content -Raw -LiteralPath $full } else { '' } }
$problems = Test-TreeFiles $files $read $cfg $prPaths $sourcesDiff $Pr
$problems | ForEach-Object { Write-Host "check-tree: $_" }
exit ([int][bool]$problems)
