# Leaves a finished task's worktree. Refuses while anything is uncommitted or on no origin ref (push and open the PR
# first). A slot (<slotPrefix>-slot<N>, <slotPrefix>-<unitySlot>) is released: detached, its local branch deleted,
# unlocked, bin/obj and Library kept warm. Any other worktree is deleted, pruned and its branch deleted.
#   From the MAIN checkout (a shell standing inside the worktree pins it and the delete fails):
#     pwsh -File tools/worktree-done.ps1 -Path <worktree>
#   Every worktree nobody uses (the loop runs this after each run):
#     pwsh -File tools/worktree-done.ps1 -Sweep
# -Sweep never touches the lane's Unity slots, the testbed or loop slot, the loop clone or its reviewer worktrees.
# Exit 0 done, 1 refused (the message says why).
param([string]$Path, [switch]$Sweep)
. (Join-Path $PSScriptRoot 'lib/UnidadTools.ps1')
$main = (Get-MainCheckout $PSScriptRoot) -replace '/', '\'
$cfg = Get-RepoConfig $main
$mainBranch = $cfg.mainBranch
$prefix = $cfg.slotPrefix
$laneOwned = @($cfg.unitySlots + $cfg.testbedSlot + $cfg.loopSlot | ForEach-Object { "$prefix-$_" })

if ($Sweep) {
    # A branch worktree goes only once its branch has MERGED (its HEAD reached main through a merge, not as a main
    # commit); nothing goes while a process runs from it or names it.
    git -C $main fetch -q --prune origin
    $mainLine = @(git -C $main rev-list --first-parent "origin/$mainBranch")
    $processes = @(Get-CimInstance Win32_Process | ForEach-Object { "$($_.ExecutablePath) $($_.CommandLine)" })
    foreach ($block in ((git -C $main worktree list --porcelain) -join "`n") -split "`n`n") {
        $lines = @($block -split "`n")
        $wt = $lines[0] -replace '^worktree ', ''
        $p = $wt -replace '/', '\'
        if (!$wt -or $p -ieq $main) { continue }
        $leaf = Split-Path $p -Leaf
        if ($laneOwned -contains $leaf) { "kept $p (lane-owned)"; continue }
        if ($block -match '(?m)^locked (claimed by .*)$') {
            # A slot claim whose agent died without releasing it: after 12 h, released if its work is pushed.
            $claim = ConvertFrom-ClaimReason $Matches[1]
            if ($claim -and $claim.At -gt (Get-Date).AddHours(-12)) { "kept $p (claimed)"; continue }
            $out = & pwsh -NoProfile -File $PSCommandPath -Path $p 2>&1
            "$out"
            continue
        }
        if ($block -match '(?m)^locked') {
            # Claude Code locks an agent's worktree with its pid; a dead pid is a stale lock. Any other lock is someone's.
            if ($block -notmatch '\(pid (\d+)\)' -or (Get-Process -Id $Matches[1] -ErrorAction SilentlyContinue)) { "kept $p (locked)"; continue }
            git -C $main worktree unlock $wt
        }
        if ($processes | Where-Object { $_.IndexOf($p, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $_.IndexOf($wt, [StringComparison]::OrdinalIgnoreCase) -ge 0 }) { "kept $p (a process runs from it or opens it)"; continue }
        $head = "$($lines | Where-Object { $_ -like 'HEAD *' })" -replace '^HEAD ', ''
        if ($block -match '(?m)^detached') {
            if ($leaf -notlike "$prefix-slot*") { "kept $p (detached, not a slot)" }
            continue   # a released slot stays for the next claim
        } else {
            git -C $main merge-base --is-ancestor $head "origin/$mainBranch" 2>$null
            if ($LASTEXITCODE -ne 0 -or $mainLine -contains $head) { "kept $p (its branch has not merged)"; continue }
        }
        $out = & pwsh -NoProfile -File $PSCommandPath -Path $p 2>&1
        "$out"
    }
    git -C $main worktree prune
    exit 0
}

if (!$Path) { Write-Host 'usage: worktree-done.ps1 -Path <worktree> | -Sweep'; exit 2 }
if (!(Test-Path -LiteralPath $Path)) { git -C $main worktree prune; "$Path is already gone; nothing to remove"; exit 0 }
$Path = (Resolve-Path -LiteralPath $Path).Path.TrimEnd('\')
if ($main -ieq $Path) { Write-Host 'that is the main checkout; name a worktree'; exit 1 }
$slash = $Path -replace '\\', '/'
$registered = [bool](git -C $main worktree list --porcelain | Where-Object { $_ -ieq "worktree $slash" })
$branchName = $null
if ($registered) {
    $branchName = git -C $Path branch --show-current
    $dirty = @(git -C $Path status --porcelain)
    if ($dirty) { Write-Host "$Path has uncommitted work; commit and push it first:`n$($dirty -join "`n")"; exit 1 }
    if (git -C $Path rev-list HEAD --not --remotes=origin -n 1) { Write-Host "$Path has commits on no origin branch; push and open the PR first"; exit 1 }
    $leaf = Split-Path $Path -Leaf
    if ($leaf -like "$prefix-slot*" -or ($cfg.unitySlots | Where-Object { $leaf -eq "$prefix-$_" })) {
        git -C $Path checkout -q -f --detach
        if ($branchName -and $branchName -ne $mainBranch) { git -C $main branch -D $branchName *> $null }
        git -C $main worktree unlock $Path 2>$null
        "released slot $Path$(if ($branchName) { " (branch $branchName, pushed)" })"
        exit 0
    }
} else {
    "$Path is not a registered worktree; deleting the leftover directory"
}
Set-Location $main
foreach ($try in 1..3) {
    try { [System.IO.Directory]::Delete("\\?\$Path", $true); break }
    catch {
        if ($try -eq 3) { Write-Host "could not delete $Path ($($_.Exception.InnerException.Message)); a shell or process is standing inside it: cd out and rerun"; exit 1 }
        # Idle MSBuild/VBCSCompiler nodes can hold obj/ open for minutes after a build: ask them to stop (graceful).
        if ($try -eq 1) {
            try { & (Resolve-UnityDotnet (Resolve-UnityEditor $null $null $cfg.editorVersion)) build-server shutdown *> $null } catch { }
        }
        Start-Sleep 2
    }
}
git -C $main worktree prune
if ($branchName -and $branchName -ne $mainBranch) { git -C $main branch -D $branchName *> $null }
"removed worktree $Path$(if ($branchName) { " (branch $branchName, pushed)" })"
