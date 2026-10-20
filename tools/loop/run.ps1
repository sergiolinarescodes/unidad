# One run of the review-and-merge loop (U-45, U-48). Scheduled by tools/loop/install.ps1; by hand:
#   pwsh -File tools/loop/run.ps1 [-Provider claude|codex] [-Clone <dir>] [-Main <dir>] [-DryRun]
# Phase A (parallel, up to loop.reviewers): one headless agent per open PR, each in its own reviewer worktree
#   (<clone>-r<k>), with tools/loop/review-merge.md as its brief; it merges main in, reviews, fixes, gates, pushes the PR
#   head and writes a verdict JSON.
# Phase B (this script, serial, in the clone): merge every PR whose verdict passed at its current head into
#   loop/staging in PR order, `gate.ps1 -BuildOnly` after each merge; then every repo.json stagingChecks command and,
#   when a merged PR touched loop.lanePaths and the lane exists, one lane -Compile and -Bee of staging; a red step is
#   bisected by PR (Find-FirstRed) and the culprit gets loop:blocked; then a fast-forward push to main, stacked PRs
#   retargeted, merged heads deleted, the refpack refreshed, worktree-done -Sweep.
# -DryRun lists the PRs and the steps it would run; it spawns no agent and pushes, labels and comments nothing.
# Logs: <clone>/.loop/<stamp>.log, verdicts and agent logs in <clone>/.loop/<stamp>/. One run at a time.
param(
    [ValidateSet('claude', 'codex')][string]$Provider,
    [string]$Clone,
    [string]$Main,
    [switch]$DryRun
)
. (Join-Path $PSScriptRoot '../lib/UnidadTools.ps1')
$here = Get-RepoRoot $PSScriptRoot
$cfg = Get-RepoConfig $here
if (!$Main) {
    # Run from the clone (the scheduled task) the main checkout is its sibling <name>; run from a checkout, it is that one's.
    $Main = if ((Split-Path $here -Leaf) -eq "$($cfg.name)-loop") { (Join-Path (Split-Path $here -Parent) $cfg.name) -replace '\\', '/' } else { Get-MainCheckout $here }
}
if (!$Clone) { $Clone = (Join-Path (Split-Path $Main -Parent) "$($cfg.name)-loop") -replace '\\', '/' }
if (!$Provider) { $Provider = $cfg.loop.provider }
$mainBranch = $cfg.mainBranch
$repo = $cfg.github
$hasClone = Test-Path (Join-Path $Clone '.git')
if (!$hasClone -and !$DryRun) { throw "loop clone missing at $Clone - run tools/loop/install.ps1" }
$work = if ($hasClone) { $Clone } else { $Main }   # a dry run before install reads from the main checkout

$state = Join-Path $work '.loop'
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
$runDir = Join-Path $state $stamp
$log = Join-Path $state "$stamp.log"
if (!$DryRun) { New-Item -ItemType Directory -Force $runDir | Out-Null }
function Log([string]$Msg) {
    $line = "$(Get-Date -Format s) $Msg"
    Write-Host $line
    if (!$DryRun) { Add-Content -LiteralPath $log -Value $line }
}
function Invoke-Gh { $out = & gh @args 2>&1; if ($LASTEXITCODE -ne 0) { Log "gh $($args -join ' ') failed: $out" }; $out }
function G { $out = & git -C $work @args 2>&1; $script:gitCode = $LASTEXITCODE; $out }

# One exclusive file handle is the mutex; the OS drops it if the run dies.
$lock = $null
if (!$DryRun) {
    try { $lock = [IO.File]::Open((Join-Path $state 'lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { Log 'skipped: another run holds the lock'; exit 0 }
}
$exitCode = 0
try {
    # 1. Sync the clone to origin/<main>.
    G fetch -q --prune origin | Out-Null
    if ($hasClone -and !$DryRun) {
        G checkout -q -B $mainBranch "origin/$mainBranch" | Out-Null
        G reset -q --hard "origin/$mainBranch" | Out-Null
        G clean -qfd -e .loop | Out-Null
    }
    Log "start | $Provider | $repo $mainBranch at $(G rev-parse --short "origin/$mainBranch")$(if ($DryRun) { ' | dry run' })"

    # 2. Candidates: open, not drafts, based on main, oldest first; stranded PRs recovered first.
    $closed = (Invoke-Gh pr list --repo $repo --state closed --limit 30 --json 'number,baseRefName,headRefName,comments,mergedAt,title') | ConvertFrom-Json
    foreach ($c in @($closed | Where-Object { !$_.mergedAt -and @($_.comments).Count -eq 0 -and $_.baseRefName -ne $mainBranch })) {
        if (G ls-remote --heads origin $c.baseRefName) { continue }
        if (!(G ls-remote --heads origin $c.headRefName)) { continue }
        G fetch -q origin $c.headRefName | Out-Null
        if ([int](G rev-list --count "origin/$mainBranch..origin/$($c.headRefName)") -eq 0) { continue }
        if ($DryRun) { Log "would recover stranded #$($c.number) ($($c.headRefName))"; continue }
        Invoke-Gh pr create --repo $repo --base $mainBranch --head $c.headRefName --title $c.title --body "Recovered from #$($c.number)" | Out-Null
        Log "recovered stranded #$($c.number) ($($c.headRefName))"
    }
    $blockedFile = Join-Path $state 'blocked.json'
    $blocked = if (Test-Path $blockedFile) { Get-Content -Raw $blockedFile | ConvertFrom-Json -AsHashtable } else { @{} }
    $open = (Invoke-Gh pr list --repo $repo --state open --base $mainBranch --json 'number,headRefName,headRefOid,isDraft,title,labels') | ConvertFrom-Json
    $candidates = @()
    foreach ($pr in @($open | Sort-Object number)) {
        if ($pr.isDraft) { Log "#$($pr.number) skipped: draft"; continue }
        if (@($pr.labels.name) -contains 'loop:blocked') {
            if ($blocked["$($pr.number)"] -eq $pr.headRefOid) { Log "#$($pr.number) skipped: loop:blocked at $($pr.headRefOid.Substring(0,7))"; continue }
            if (!$DryRun) { Invoke-Gh pr edit $pr.number --repo $repo --remove-label 'loop:blocked' | Out-Null; $blocked.Remove("$($pr.number)") }
            Log "#$($pr.number): new push since it was blocked; label lifted"
        }
        $candidates += $pr
    }
    $reviewers = [Math]::Min([int]$cfg.loop.reviewers, [Math]::Max(1, $candidates.Count))
    $wts = @(1..$reviewers | ForEach-Object { "$Clone-r$_" })
    $laneScript = Join-Path $Main 'tools/unity-lane.ps1'
    $hasLane = Test-Path $laneScript

    if ($DryRun) {
        if (!$candidates) { Log 'plan: no open PR to review' }
        foreach ($i in 0..($candidates.Count - 1)) {
            if (!$candidates) { break }
            $pr = $candidates[$i]
            Log "plan: review #$($pr.number) '$($pr.title)' ($($pr.headRefName) at $($pr.headRefOid.Substring(0,7))) in $($wts[$i % $wts.Count])"
        }
        Log "plan: Phase A $Provider/$($cfg.loop.model), up to $reviewers reviewers; Phase B staging merge + gate.ps1 -BuildOnly per PR"
        foreach ($c in $cfg.stagingChecks) { Log "plan: staging check '$($c.run)'$(if ($c.requires -and !(Test-Path (Join-Path $work $c.requires))) { ' (skipped: ' + $c.requires + ' missing)' })" }
        Log "plan: lane step $(if ($hasLane) { "-Compile and -Bee on slot $($cfg.loopSlot) when a merged PR matches loop.lanePaths" } else { 'skipped (tools/unity-lane.ps1 not in the main checkout yet)' })"
        Log "plan: push loop/staging to $mainBranch, retarget stacked PRs, delete merged heads, worktree-done -Sweep"
        exit 0
    }
    if (!$candidates) { Log 'nothing to review' }

    # 3. Phase A: parallel reviews, each in its own reviewer worktree of the clone.
    foreach ($wt in $wts) { if (!(Test-Path $wt)) { G worktree add -q --detach $wt "origin/$mainBranch" | Out-Null } }
    $brief = Get-Content -Raw (Join-Path $Clone 'tools/loop/review-merge.md')
    $queue = [Collections.Concurrent.ConcurrentQueue[string]]::new()
    $wts | ForEach-Object { $queue.Enqueue($_) }
    $env:CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS = '0'   # never cap an agent by wall time: a killed run throws its work away
    $candidates | ForEach-Object -ThrottleLimit $reviewers -Parallel {
        $pr = $_; $q = $using:queue; $wt = $null
        while (!$q.TryDequeue([ref]$wt)) { Start-Sleep 1 }
        try {
            $agentLog = Join-Path $using:runDir "pr-$($pr.number).log"
            git -C $wt fetch -q origin 2>&1 | Out-Null
            git -C $wt checkout -q -f -B "pr-$($pr.number)" "origin/$($pr.headRefName)" 2>&1 | Out-Null
            $header = @(
                "PR #$($pr.number): $($pr.title)", "Head branch: $($pr.headRefName)", "Worktree: $wt",
                "Verdict file: $(Join-Path $using:runDir "pr-$($pr.number).json")", "Main branch: $($using:mainBranch)",
                "Repository: $($using:repo)", '', '---', '') -join "`n"
            Push-Location $wt
            try {
                if ($using:Provider -eq 'claude') {
                    $extra = @($using:cfg.loop.extraArgs)
                    ($header + $using:brief) | claude -p --model $using:cfg.loop.model --permission-mode bypassPermissions --output-format text @extra *> $agentLog
                } else {
                    ($header + $using:brief) | codex exec --full-auto -C $wt - *> $agentLog
                }
            } finally { Pop-Location }
        } finally { $q.Enqueue($wt) }
    }

    # 4. Phase B: scripted staging merge in PR order.
    G fetch -q --prune origin | Out-Null
    G checkout -q -B loop/staging "origin/$mainBranch" | Out-Null
    $merged = [Collections.Generic.List[object]]::new()
    $results = [ordered]@{}
    function Block($Pr, [string]$Head, [string]$Why) {
        Invoke-Gh pr edit $Pr.number --repo $repo --add-label 'loop:blocked' | Out-Null
        $blocked["$($Pr.number)"] = $Head
        Invoke-Gh pr comment $Pr.number --repo $repo --body "The merge loop blocked this head ($Head). A new push lifts the block.`n`n``````n$Why`n``````" | Out-Null
        $results["#$($Pr.number)"] = "blocked: $(($Why -split "`n")[0])"
    }
    foreach ($pr in $candidates) {
        $vf = Join-Path $runDir "pr-$($pr.number).json"
        $v = if (Test-Path $vf) { try { Get-Content -Raw $vf | ConvertFrom-Json } catch { $null } }
        $current = (G rev-parse "origin/$($pr.headRefName)") | Select-Object -First 1
        if ($v -and $v.verdict -eq 'blocked' -and $v.head -eq $current) { Block $pr $current "review verdict: $($v.reason)"; continue }
        if (!(Test-VerdictUsable $v $current)) { $results["#$($pr.number)"] = "skipped: $(if (!$v) { 'no verdict' } else { "verdict $($v.verdict) at $($v.head), head now $current" })"; continue }
        $out = G merge --no-ff --no-edit -m "Merge #$($pr.number) ($($pr.headRefName)) [loop]" $v.head
        if ($gitCode -ne 0) {
            G merge --abort | Out-Null
            Invoke-Gh pr comment $pr.number --repo $repo --body 'This head conflicts with a PR merged earlier in this loop run; the next run resolves it.' | Out-Null
            $results["#$($pr.number)"] = 'skipped: conflicts with an earlier merge of this run'
            continue
        }
        $gate = & pwsh -NoProfile -File (Join-Path $Clone 'tools/gate.ps1') -BuildOnly -Root $Clone 2>&1
        if ($LASTEXITCODE -ne 0) {
            G reset -q --hard HEAD~1 | Out-Null
            Block $pr $current ((@($gate) | Select-Object -Last 40) -join "`n")
            continue
        }
        $merged.Add([pscustomobject]@{ Pr = $pr; Head = $current; Sha = ((G rev-parse HEAD) | Select-Object -First 1); Paths = @(G -c core.quotepath=off diff --name-only HEAD~1 HEAD) })
        $results["#$($pr.number)"] = 'merged'
    }

    # 5. Staging checks and the lane, bisected by PR on red.
    $dotnet = try { Resolve-UnityDotnet (Resolve-UnityEditor $null $null $cfg.editorVersion) } catch { 'dotnet' }
    $laneGreen = $false
    $abort = $false
    function Invoke-Lane([string]$Mode, [string]$Sha, [string]$Ref) {
        G push -q -f origin "${Sha}:refs/heads/$Ref" | Out-Null
        $out = & pwsh -NoProfile -File $laneScript "-$Mode" -Branch $Ref -Slot $cfg.loopSlot 2>&1
        $code = $LASTEXITCODE
        Add-Content -LiteralPath $log -Value $out
        if ($code -ge 2) { $script:abort = $true; Log "lane -$Mode exit $code (queue timeout, STOP or environment): nothing is pushed this run" }
        $script:lastStepOutput = (@($out) | Select-Object -Last 40) -join "`n"
        $code -eq 0
    }
    function Test-Step($Step, [string]$Sha) {
        if ($Step.Lane) { return Invoke-Lane $Step.Lane $Sha 'loop/bisect' }
        G checkout -q --detach $Sha | Out-Null
        $ok = (Invoke-RepoCommand $Step.Name $Clone $dotnet -Quiet) -eq 0
        $script:lastStepOutput = (@($script:LastCommandOutput) | Select-Object -Last 40) -join "`n"
        G checkout -q loop/staging | Out-Null
        $ok
    }
    $steps = @()
    foreach ($c in $cfg.stagingChecks) {
        if ($c.requires -and !(Test-Path (Join-Path $Clone $c.requires))) { Log "staging check '$($c.run)' skipped ($($c.requires) missing)"; continue }
        $steps += [pscustomobject]@{ Name = $c.run; Lane = $null }
    }
    $laneNeeded = $hasLane -and (Test-MatchesAny @($merged | ForEach-Object { $_.Paths }) @($cfg.loop.lanePaths))
    if ($laneNeeded) {
        $steps += [pscustomobject]@{ Name = 'lane -Compile'; Lane = 'Compile' }
        $steps += [pscustomobject]@{ Name = 'lane -Bee'; Lane = 'Bee' }
    } elseif (!$hasLane -and $merged.Count) { Log 'lane step skipped: tools/unity-lane.ps1 is not in the main checkout yet' }
    $again = $true
    while ($again -and $merged.Count -and !$abort) {
        $again = $false
        foreach ($s in $steps) {
            if (Test-Step $s $merged[-1].Sha) { continue }
            if ($abort) { break }
            $i = if ($merged.Count -eq 1) { 1 } else { Find-FirstRed $merged.Count { param($k) if ($abort) { return $true }; Test-Step $s $merged[$k - 1].Sha } }
            if ($abort) { break }
            $culprit = $merged[$i - 1]
            Block $culprit.Pr $culprit.Head "$($s.Name) is red with this PR merged:`n$lastStepOutput"
            foreach ($later in @($merged | Select-Object -Skip $i)) { $results["#$($later.Pr.number)"] = "skipped: after #$($culprit.Pr.number), which turned $($s.Name) red" }
            $keep = @($merged | Select-Object -First ($i - 1))
            $merged.Clear(); $keep | ForEach-Object { $merged.Add($_) }
            G reset -q --hard $(if ($merged.Count) { $merged[-1].Sha } else { "origin/$mainBranch" }) | Out-Null
            $again = $true
            break
        }
    }
    if ($steps | Where-Object Lane) { G push -q origin --delete loop/bisect 2>$null | Out-Null; $laneGreen = !$abort -and $merged.Count -gt 0 }

    # 6. Push staging to main (fast-forward only), retarget stacked PRs, then delete merged heads.
    if ($abort) { foreach ($m in $merged) { $results["#$($m.Pr.number)"] = 'skipped: lane unavailable this run' }; $merged.Clear() }
    if ($merged.Count) {
        G push -q origin "loop/staging:$mainBranch" | Out-Null
        if ($gitCode -ne 0) {
            Log "push to $mainBranch rejected (main moved); the next run retries"
            foreach ($m in $merged) { $results["#$($m.Pr.number)"] = 'skipped: push rejected' }
            $merged.Clear()
        } else {
            foreach ($m in $merged) {
                foreach ($s in @((Invoke-Gh pr list --repo $repo --state open --base $m.Pr.headRefName --json number) | ConvertFrom-Json)) {
                    Invoke-Gh pr edit $s.number --repo $repo --base $mainBranch | Out-Null
                }
                G push -q origin --delete $m.Pr.headRefName | Out-Null
            }
            # 7. Refresh the reference pack from the loop slot.
            $refpack = Join-Path $Main 'tools/refpack.ps1'
            if ($laneGreen -and (Test-Path $refpack)) {
                & pwsh -NoProfile -File $refpack -Slot $cfg.loopSlot -Sha ((G rev-parse "loop/staging") | Select-Object -First 1) 2>&1 | Add-Content -LiteralPath $log
            }
        }
    }
    $blocked | ConvertTo-Json | Set-Content -LiteralPath $blockedFile

    # 8. Leave the main checkout clean.
    $done = Join-Path $Main 'tools/worktree-done.ps1'
    if (Test-Path $done) { & pwsh -NoProfile -File $done -Sweep 2>&1 | Add-Content -LiteralPath $log }
    foreach ($k in $results.Keys) { Log "$k $($results[$k])" }
} catch {
    Log "run failed: $($_.Exception.Message)"
    $exitCode = 1
} finally {
    if ($lock) { $lock.Dispose() }
}
Log "end | exit $exitCode"
exit $exitCode
