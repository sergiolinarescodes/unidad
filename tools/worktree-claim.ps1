# Claims a reusable slot beside the main checkout and puts <branch> on it, cut from <base>. Prints the slot's path as
# the last line; every later command of the task runs there. Slot names, count and main branch come from tools/repo.json.
#   pwsh -File tools/worktree-claim.ps1 -Branch <b> [-Base origin/<x>] [-Kind code|unity] [-Slots N] [-WaitMinutes 9]
# Kind code (default): <parent>/<slotPrefix>-slot<N>, no Library, never runs Unity. Kind unity: the repo's unitySlots
# (<parent>/<slotPrefix>-unity1, ...), for the lane and asset work. A slot keeps its bin/obj (and Library) warm between
# tasks: nothing is ever `git clean`ed. The claim is a `git worktree lock`, which git refuses twice.
# Exit 0 claimed, 2 bad base / not a checkout, 3 every slot busy after the wait (run it again).
# Release, from the MAIN checkout: pwsh -File tools/worktree-done.ps1 -Path <slot>
param(
    [Parameter(Mandatory)][string]$Branch,
    [string]$Base,
    [ValidateSet('code', 'unity')][string]$Kind = 'code',
    [int]$Slots = 0,
    [int]$WaitMinutes = 9
)
. (Join-Path $PSScriptRoot 'lib/UnidadTools.ps1')
try { $main = Get-MainCheckout $PSScriptRoot } catch { Write-Host 'run this from a checkout of the repository'; exit 2 }
$cfg = Get-RepoConfig $main
if (!$Base) { $Base = "origin/$($cfg.mainBranch)" }
git -C $main fetch -q origin
if ($LASTEXITCODE -ne 0) { Write-Host 'git fetch origin failed'; exit 2 }
git -C $main rev-parse -q --verify "$Base^{commit}" *> $null
if ($LASTEXITCODE -ne 0) { Write-Host "base $Base does not exist (fetch it, or name origin/<branch>)"; exit 2 }

# Repo config, shared by every worktree: the pre-commit hook (metas + parity) and Unity's YAML merge driver.
git -C $main config core.hooksPath .githooks
try {
    $yaml = Resolve-YamlMerge (Resolve-UnityEditor (Get-UnityProjectPath $cfg $main) $null $cfg.editorVersion)
    git -C $main config merge.unityyamlmerge.name 'Unity YAML merge'
    git -C $main config merge.unityyamlmerge.driver "`"$yaml`" merge -p %O %B %A %A"
} catch { Write-Warning "no Unity editor installed: the unityyamlmerge driver is not set ($($_.Exception.Message))" }

$parent = Split-Path $main -Parent
$names = if ($Kind -eq 'unity') { @($cfg.unitySlots) } else { 1..$(if ($Slots -gt 0) { $Slots } else { $cfg.codeSlots }) | ForEach-Object { "slot$_" } }
$deadline = (Get-Date).AddMinutes($WaitMinutes)
$warned = @{}
while ($true) {
    foreach ($n in $names) {
        $slot = Get-SlotPath $cfg $parent $n
        if (!(Test-Path -LiteralPath $slot)) {
            git -C $main worktree add -q --detach $slot $Base 2>$null
            if ($LASTEXITCODE -ne 0) { continue }   # another claimer made it first
        }
        git -C $main worktree lock --reason (Format-ClaimReason $Branch) $slot 2>$null
        if ($LASTEXITCODE -ne 0) { continue }
        $dirty = @(git -C $slot status --porcelain)
        if ($dirty) {
            git -C $main worktree unlock $slot
            if (!$warned[$slot]) { Write-Warning "$slot is unclaimed but has uncommitted work; skipped (look at it by hand):`n$($dirty -join "`n")"; $warned[$slot] = $true }
            continue
        }
        $out = git -C $slot checkout -q -f --no-track -B $Branch $Base 2>&1
        if ($LASTEXITCODE -ne 0) { git -C $main worktree unlock $slot; Write-Host "could not put $Branch on ${slot}: $out"; exit 2 }
        $slot
        exit 0
    }
    if ((Get-Date) -gt $deadline) { Write-Warning "all $($names.Count) $Kind slots are busy; run the claim again"; exit 3 }
    Start-Sleep 20
}
