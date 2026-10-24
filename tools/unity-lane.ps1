<#
The serial Unity lane (U-10, U-61): the only way an agent starts Unity.exe.
  unity-lane.ps1 -Compile -Branch <b> [-Slot <s>]                metas + parity, regenerate when the asmdef/.cs list changed,
                                                                editor-free MSBuild-mode build, repo.json postCompileChecks
  unity-lane.ps1 -Bee     -Branch <b> [-Slot <s>]                Bee batch compile (log-parsed unless beeExitCodeReliable)
  unity-lane.ps1 -Import  -Branch <b> [-Collect <roots>|default] batch import; -Collect copies importer output back
  unity-lane.ps1 -Exec <Method> -Branch <b> [-Collect <roots>|default] [-- <recipe args>]
  unity-lane.ps1 -Player  -Branch <b> [-Boot] [-Strict] [-Graphics] [-Start t] [-Set "k=v;..."] [-Frames n] [-Release]
  unity-lane.ps1 -EditorPlay -Branch <b> [-Strict] [-Start t] [-Set "k=v;..."] [-Frames n]
                                                                a windowed editor runs the testbed entry in Play mode for the
                                                                frames, then quits with the boot report's code
  unity-lane.ps1 -Run     -Branch <b> [-Start t] [-Set "k=v;..."] [-Frames n]   (reuses Build/ stamped with the SHA)
  unity-lane.ps1 -Claim   -Branch <b> [-Slot <s>]  /  -Release -Branch <b>        (a claim idles out after 20 min)
  unity-lane.ps1 -Init    -Slot <s> [-Branch <b>] [-From <slot>]
  unity-lane.ps1 -Status
-Graphics runs the player windowed (640x360) instead of -batchmode -nographics. -EditorPlay and every player run scan their
log: an exception, a LogError/LogException, a boot failure or "Unable to generate text" exits 1 (lane.logIgnore drops
a headline by regex). While a -Run/-Boot player or an -EditorPlay editor runs, the lane tails its log every second, prints
"lane: run t=<s> log=<lines> last=<line>" every 15 s, and ends the run itself on a boot failure ("Initialization failed"
or an exception before "Initialization complete": exit 7) or, players only, after lane.stallSeconds (default 90)
without a new log line (exit 8); a game with long silent phases raises it or logs a heartbeat. -Branch uses the local
refs/heads/<b> (commit first), else origin/<b>. Collect roots are relative to the Unity project and comma-separated.
Exit: 0 green; 1 compile/check/recipe failure; 2 usage or environment; 3 lock wait timed out or killed at the hold cap;
4 STOP; 5 the slot is open in an interactive editor; 7 the run's boot failed; 8 the run stalled.
#>
param(
    [switch]$Compile, [switch]$Bee, [switch]$Import, [string]$Exec, [switch]$Player, [switch]$Run, [switch]$EditorPlay, [switch]$Graphics,
    [switch]$Claim, [switch]$Release, [switch]$Init, [switch]$Status,
    [string]$Branch, [string]$Slot, [string[]]$Collect, [string]$From,
    [switch]$Boot, [switch]$Strict, [string]$Start, [string]$Set, [int]$Frames,
    [double]$WaitMinutes = 60, [string]$UnityPath,
    [Parameter(ValueFromRemainingArguments)][string[]]$RecipeArgs
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/Lane.ps1')

$usage = 'usage: unity-lane.ps1 -Compile|-Bee|-Import|-Exec <M>|-Player|-Run|-EditorPlay|-Claim|-Release|-Init|-Status -Branch <b> [-Slot <s>] (see the header)'
$modes = @(@($Compile, $Bee, $Import, [bool]$Exec, $Player, $Run, $EditorPlay, $Claim, $Init, $Status) | Where-Object { $_ })
$releaseClaim = $Release -and !$Player
if ($modes.Count + [int]$releaseClaim -ne 1) { Write-Host $usage; exit 2 }
if (!$Status -and !$Init -and !$Branch) { Write-Host "$usage`n-Branch is required"; exit 2 }

$main = Get-MainCheckout $PSScriptRoot
$cfg = Get-RepoConfig (Get-RepoRoot $PSScriptRoot)
$repo = $cfg.name
$parent = Split-Path $main -Parent
$caps = $cfg.lane.capsMinutes
$stall = if ($null -ne $cfg.lane.stallSeconds) { [int]$cfg.lane.stallSeconds } else { 90 }

if ($Status) {
    Show-Launches
    Show-EditorLockQueue (Get-EditorLockDir)
    foreach ($s in @($cfg.unitySlots) + $cfg.loopSlot + $cfg.testbedSlot) {
        $lock = Read-LockRecord (Get-SlotLockPath $repo $s)
        $slotClaim = Read-SlotClaim $repo $s   # not $claim: PowerShell names are case-blind and -Claim is a switch
        $path = Get-SlotPath $cfg $parent $s
        $state = if (Test-Path -LiteralPath $path) { Read-LaneState (Get-UnityProjectPath $cfg $path) } else { $null }
        Write-Host ("[slot] {0}  {1}  lock: {2}  claim: {3}  compile: {4}  bee: {5}" -f $s, $(if ($state) { $path } else { '(not created)' }),
            $(if ($lock -and !(Test-LockStale $lock)) { "$($lock.Branch) $($lock.Mode) pid $($lock.Pid) since $($lock.Started)" } else { 'free' }),
            $(if ($slotClaim) { "$($slotClaim.ClaimedBy) (last used $($slotClaim.LastUsed))" } else { 'none' }),
            $(if ($state.LastCompile) { "$($state.LastCompile.Result) at $($state.LastCompile.Sha)" } else { '-' }),
            $(if ($state.LastBee) { "$($state.LastBee.Result) at $($state.LastBee.Sha)" } else { '-' }))
    }
    exit 0
}

if (Test-LaunchStopped) { Write-Host "lane: launches are STOPPED ($(Get-StopFile)); the owner lifts it with testbed.ps1 -Allow"; exit 4 }

if ($Claim -or $releaseClaim) {
    $s = if ($Slot) { $Slot } else {
        $mine = @($cfg.unitySlots | Where-Object { (Read-SlotClaim $repo $_).ClaimedBy -eq $Branch })
        if ($mine) { $mine[0] } elseif ($Claim) { @($cfg.unitySlots | Where-Object { !(Read-SlotClaim $repo $_) })[0] }
    }
    if (!$s) { Write-Host "lane: $(if ($Claim) { 'every Unity slot is claimed' } else { "$Branch holds no claim" })"; exit $(if ($Claim) { 3 } else { 0 }) }
    $c = Read-SlotClaim $repo $s
    if ($Claim) {
        if ($c -and $c.ClaimedBy -ne $Branch) { Write-Host "lane: $s is claimed by $($c.ClaimedBy)"; exit 3 }
        Write-SlotClaim $repo $s $Branch
        Write-Host "lane: $s claimed by $Branch (released after $script:ClaimIdleMinutes min without a lane call)"
    } elseif ($c -and $c.ClaimedBy -eq $Branch) {
        Remove-Item -LiteralPath (Get-SlotClaimPath $repo $s) -Force
        Write-Host "lane: claim of $s by $Branch released"
    }
    exit 0
}

# --- commit and slot ---
$target = if ($Branch) { $Branch } else { $cfg.mainBranch }
git -C $main fetch -q origin $target 2>$null
$commit = Resolve-LaneCommit $main $target
if (!$commit) { Write-Host "lane: no local or origin branch '$target'"; exit 2 }
$sha = $commit.Sha
Write-Host "lane: $target = $sha ($($commit.Source))"

$mode = if ($Exec) { 'exec' } elseif ($Compile) { 'compile' } elseif ($Bee) { 'bee' } elseif ($Import) { 'import' } elseif ($Player) { 'player' } elseif ($Run) { 'run' } elseif ($EditorPlay) { 'editorplay' } else { 'init' }
$candidates = if ($Slot) { @($Slot) } else {
    $mine = @(); $free = @(); $borrow = @()
    foreach ($s in $cfg.unitySlots) {
        $c = Read-SlotClaim $repo $s
        if (!$c) { $free += $s } elseif ($c.ClaimedBy -eq $target) { $mine += $s } elseif ($mode -eq 'compile') { $borrow += $s }
    }
    $mine + $free + $borrow
}
if (!$candidates) { Write-Host "lane: every Unity slot is claimed by another branch"; exit 3 }
$slotName = $null
$deadline = [DateTime]::UtcNow.AddMinutes($WaitMinutes)
while (!$slotName) {
    foreach ($s in $candidates) { if (Enter-SlotLock $repo $s $target $mode) { $slotName = $s; break } }
    if ($slotName) { break }
    if ([DateTime]::UtcNow -ge $deadline) { Write-Host "lane: waited $WaitMinutes min for a free slot among $($candidates -join ', ')"; exit 3 }
    Start-Sleep -Seconds 2
}

$exitCode = 2
try {
    $slotPath = Get-SlotPath $cfg $parent $slotName
    if ($slotPath -match '--') { Write-Host "lane: $slotPath contains '--' (MSBuild mode breaks on it)"; exit 2 }
    $fresh = $false
    if (!(Test-Path -LiteralPath (Join-Path $slotPath '.git'))) {
        git -C $main worktree prune
        git -C $main worktree add -q --detach $slotPath $sha
        if ($LASTEXITCODE -ne 0) { Write-Host "lane: could not create the slot worktree $slotPath"; exit 2 }
        $fresh = $true
    }
    $proj = Get-UnityProjectPath $cfg $slotPath
    if (Test-ProjectOpen $proj) { Write-Host "lane: $proj is open in an interactive editor; close it first"; exit 5 }
    git -C $slotPath checkout -q --force --detach $sha
    if ($LASTEXITCODE -ne 0) { Write-Host "lane: checkout of $sha failed in $slotPath"; exit 2 }
    if ($cfg.lane.prepare -and (Test-Path -LiteralPath (Join-Path $slotPath $cfg.lane.prepare))) {
        Push-Location $slotPath
        try { & pwsh -NoProfile -File $cfg.lane.prepare | Out-Host } finally { Pop-Location }
        if ($LASTEXITCODE -ne 0) { Write-Host "lane: $($cfg.lane.prepare) failed"; exit 1 }
    }
    $claimRec = Read-SlotClaim $repo $slotName
    if ($claimRec -and $claimRec.ClaimedBy -eq $target) { Write-SlotClaim $repo $slotName $target }

    Set-SearchIndexStartupOff $proj
    $editor = try { Resolve-UnityEditor $proj $UnityPath $cfg.editorVersion } catch { Write-Host "lane: $($_.Exception.Message)"; exit 2 }
    $dotnet = Resolve-UnityDotnet $editor
    # inside sandbox-gate's hold, re-enter it (U-55)
    $owner = if ($env:UNIDAD_EDITOR_LOCK_OWNER) { $env:UNIDAD_EDITOR_LOCK_OWNER } else { "lane:${repo}:${slotName}:$PID" }
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    New-Item -ItemType Directory -Force (Join-Path $proj 'Logs') | Out-Null
    function LogFile([string]$m) { "$proj/Logs/lane-$m-$stamp.log" }
    function Unity([string]$m, [int]$cap, [string[]]$a) { Invoke-LaneUnity $editor $a $owner $cap $slotPath $target $m $WaitMinutes $script:MsbuildEnv }
    $state = Read-LaneState $proj

    function Open-Msbuild([string]$m, [int]$cap) {
        $code = Unity $m $cap (Get-UnityArgs 'regenerate' $proj (LogFile $m))
        if ($code -lt 3 -and (Get-ChildItem -LiteralPath $proj -Filter '*.slnx' -ErrorAction SilentlyContinue)) {
            $script:state['GeneratedListHash'] = Get-CheckoutListHash $slotPath ([bool]$cfg.lane.msbuildGlobs)
            $script:state['GeneratedAt'] = [DateTime]::UtcNow.ToString('o')
        }
        $code
    }

    # A new slot, or -Init: a copied Library (never a symlink), then one MSBuild-mode open.
    if ($Init -or $fresh -or !(Test-Path -LiteralPath (Join-Path $proj 'Library'))) {
        $src = if ($From) { Get-UnityProjectPath $cfg (Get-SlotPath $cfg $parent $From) } else {
            @($cfg.unitySlots + $cfg.loopSlot + $cfg.testbedSlot | Where-Object { $_ -ne $slotName } | ForEach-Object { Get-UnityProjectPath $cfg (Get-SlotPath $cfg $parent $_) } |
                Where-Object { Test-Path -LiteralPath (Join-Path $_ 'Library/MSBuild') }) | Select-Object -First 1
        }
        if ($src -and !(Test-Path -LiteralPath (Join-Path $proj 'Library'))) {
            Write-Host "lane: seeding $proj/Library from $src"
            robocopy "$src/Library" "$proj/Library" /MIR /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
            if ($LASTEXITCODE -ge 8) { Write-Host "lane: robocopy failed ($LASTEXITCODE)"; exit 2 }
        }
        Write-Host "lane: one-time MSBuild-mode open of $proj"
        $code = Open-Msbuild 'init' $caps.init
        Write-LaneState $proj $state
        if ($Init) { $exitCode = if ($code -ge 3) { $code } elseif (Get-ChildItem -LiteralPath $proj -Filter '*.slnx') { 0 } else { 1 }; exit $exitCode }
        if ($code -ge 3) { $exitCode = $code; exit $exitCode }
    }

    function Invoke-Collect([string[]]$Roots) {
        $callerRoot = Get-RepoRoot (Get-Location).Path
        if ([IO.Path]::GetFullPath($callerRoot) -in @([IO.Path]::GetFullPath($main), [IO.Path]::GetFullPath($slotPath))) {
            Write-Host "lane: -Collect copies into the caller's worktree; run it from your code slot, not $callerRoot"; return 2
        }
        $repoRoots = @(Resolve-CollectRoots @($Roots | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) $cfg.projectSubdir)
        $lines = @(git -C $slotPath -c core.quotepath=off status --porcelain=v1 --untracked-files=all -- @repoRoots)
        foreach ($c in Select-CollectPaths $lines $repoRoots) {
            $dst = Join-Path $callerRoot $c.Path
            if ($c.Deleted) { Remove-Item -LiteralPath $dst -Force -ErrorAction SilentlyContinue; Write-Host "lane: collected (deleted) $($c.Path)" }
            else { New-Item -ItemType Directory -Force (Split-Path $dst -Parent) | Out-Null; Copy-Item -LiteralPath (Join-Path $slotPath $c.Path) $dst -Force; Write-Host "lane: collected $($c.Path)" }
        }
        git -C $slotPath checkout -q --force --detach $sha
        git -C $slotPath clean -qfd -- @repoRoots
        0
    }
    # The boot line every self-quitting run shares: playerBootArgs (frames, fresh, report) under -Start/-Set/-Frames/-Strict.
    function BootLine([string]$Report) {
        $base = @($cfg.lane.playerBootArgs | ForEach-Object { $_.Replace('{report}', ($Report -replace '\\', '/')) })
        $extra = if ($Strict) { @{ strict = 'true' } } else { @{} }
        ConvertTo-LaunchLine -Start $Start -Set $Set -Frames $Frames -Base $base -Extra $extra
    }
    function NewReport { $r = Join-Path ([IO.Path]::GetTempPath()) "unidad-lane/$stamp-report.json"; New-Item -ItemType Directory -Force (Split-Path $r -Parent) | Out-Null; $r }
    # Prints the report and the log's error headlines; 7 on a boot failure the run quit on itself, 1 on a headline, else
    # the run's own code.
    function Close-Run([string]$Report, [string]$Log, [int]$Code) {
        if (Test-Path -LiteralPath $Report) { Get-Content -LiteralPath $Report | Out-Host }
        $lines = @(if (Test-Path -LiteralPath $Log) { Get-Content -LiteralPath $Log })
        $hits = @(Get-LogErrors $lines @($cfg.lane.logIgnore))
        $hits | ForEach-Object { Write-Host "lane: log error: $_" }
        if ($Code -lt 3 -and ($v = Get-RunVerdict $lines 0 0 -Ignore @($cfg.lane.logIgnore))) { Write-Host "lane: $($v.Why)"; $Code = $v.Code }
        Write-Host "lane: exit $Code, $($hits.Count) log error(s) (log $Log)"
        if ($hits -and $Code -lt 3) { 1 } else { $Code }
    }
    $collectRoots = if ($Collect -and ($Collect -join ',') -ne 'default') { $Collect } elseif ($Collect) { @($cfg.lane.collectRoots) }

    switch ($mode) {
        'compile' {
            Push-Location $slotPath
            try {
                & bash tools/unity-metas.sh --check | Out-Host; $m = $LASTEXITCODE
                & pwsh -NoProfile -File tools/asmdef-parity.ps1 | Out-Host; $p = $LASTEXITCODE
            } finally { Pop-Location }
            if ($m -ne 0 -or $p -ne 0) { Write-Host 'lane: metas or asmdef parity red'; $exitCode = 1; break }
            $hash = Get-CheckoutListHash $slotPath ([bool]$cfg.lane.msbuildGlobs)
            $slnx = { Get-ChildItem -LiteralPath $proj -Filter '*.slnx' -ErrorAction SilentlyContinue | Select-Object -First 1 }
            $r = Invoke-CompileCore $state $hash { $s = & $slnx; [bool]$s -and (Test-SlnxComplete $s.FullName) -and (Test-Path -LiteralPath (Join-Path $proj 'Library/MSBuild')) } { Open-Msbuild 'regenerate' $caps.compile } {
                Write-Host "lane: dotnet build $((& $slnx).Name)"
                Invoke-Captured $dotnet @('build', (& $slnx).FullName, '-v:q', '-nologo') ($caps.compile * 60) $proj $script:MsbuildEnv
            }
            if ($r.Code -ge 3) { $exitCode = $r.Code; break }
            $r.Errors | ForEach-Object { Write-Host $_ }
            $exitCode = $r.Code
            if ($exitCode -eq 0) {
                $mb = (git -C $slotPath merge-base $sha "origin/$($cfg.mainBranch)" 2>$null)
                $changed = if ($mb) { @(git -C $slotPath diff --name-only $mb.Trim() $sha) } else { @() }
                $diff = if ($mb) { (git -C $slotPath diff $mb.Trim() $sha) -join "`n" } else { '' }
                foreach ($check in @($cfg.lane.postCompileChecks)) {
                    $hit = (Test-MatchesAny $changed @($check.whenPaths)) -or ($check.whenDiffMatches -and $diff -match $check.whenDiffMatches)
                    if (!$hit) { continue }
                    $leaf = ($check.method -split '\.')[-1]
                    git -C $slotPath grep -q -E "static\s+void\s+$leaf\s*\(" -- '*.cs' 2>$null   # only code: specs quote the signature
                    if ($LASTEXITCODE -ne 0) { Write-Warning "lane: postCompileCheck $($check.name): $($check.method) does not exist yet; skipped"; continue }
                    $code = Unity 'exec' $caps.exec (Get-UnityArgs 'exec' $proj (LogFile $check.name) -Method $check.method)
                    Write-Host "lane: postCompileCheck $($check.name) exit $code"
                    if ($code -ne 0) { $exitCode = $(if ($code -ge 3) { $code } else { 1 }); break }
                }
            }
            $state['LastCompile'] = @{ Sha = $sha; Result = $(if ($exitCode -eq 0) { 'green' } else { 'red' }); At = [DateTime]::UtcNow.ToString('o') }
        }
        'bee' {
            $log = LogFile 'bee'
            $code = Unity 'bee' $caps.compile (Get-UnityArgs 'bee' $proj $log)
            if ($code -ge 3) { $exitCode = $code; break }
            $errors = @(if (Test-Path -LiteralPath $log) { Get-CompileErrors @(Get-Content -LiteralPath $log | Where-Object { $_ -match ': error (CS|UD|UAC)\d' }) })
            $errors | ForEach-Object { Write-Host $_ }
            $exitCode = if ($code -ne 0 -or (!$cfg.lane.beeExitCodeReliable -and $errors)) { 1 } else { 0 }
            if ($exitCode) { Write-Host "lane: Bee red (exit $code); log $log" }
            $state['LastBee'] = @{ Sha = $sha; Result = $(if ($exitCode -eq 0) { 'green' } else { 'red' }); At = [DateTime]::UtcNow.ToString('o') }
        }
        { $_ -in 'import', 'exec' } {
            $log = LogFile $mode
            $a = if ($mode -eq 'import') { Get-UnityArgs 'import' $proj $log } else {
                $method = Resolve-ExecMethod $Exec $cfg.lane.execPrefix
                Get-UnityArgs 'exec' $proj $log -Method $method -Extra @($RecipeArgs | Where-Object { $_ -ne '--' })
            }
            $code = Unity $mode $caps.$mode $a
            Write-Host "lane: $mode exit $code (log $log)"
            if ($code -ge 3) { $exitCode = $code; break }
            $exitCode = if ($code -eq 0) { 0 } else { 1 }
            if ($collectRoots) { $c = Invoke-Collect $collectRoots; if ($c -ne 0) { $exitCode = $c } }
        }
        { $_ -in 'player', 'run' } {
            $buildDir = "$proj/Build"
            $exe = "$buildDir/$($cfg.lane.playerName).exe"
            $stampFile = "$buildDir/lane-stamp.txt"
            $stampText = if (Test-Path -LiteralPath $stampFile) { Get-Content -Raw -LiteralPath $stampFile }
            if ($Player -or !(Test-BuildReusable $stampText $sha (Test-Path -LiteralPath $exe))) {
                if (Test-Path -LiteralPath $stampFile) { Remove-Item -LiteralPath $stampFile -Force }
                # An MSBuild-mode compile leaves its own ScriptAssemblies, whose partial-class field order can differ from
                # Bee's player build ("script class layout is incompatible"); the build editor recompiles them with Bee.
                Remove-Item -LiteralPath "$proj/Library/ScriptAssemblies" -Recurse -Force -ErrorAction SilentlyContinue
                $code = Unity 'player' $caps.player (Get-UnityArgs 'player' $proj (LogFile 'player') -Method $cfg.lane.playerBuildMethod -BuildOut $buildDir -Release:$Release)
                if ($code -ne 0 -or !(Test-Path -LiteralPath $exe)) { Write-Host "lane: player build exit $code"; $exitCode = $(if ($code -ge 3) { $code } else { 1 }); break }
                [IO.File]::WriteAllText($stampFile, $sha)
            } else { Write-Host "lane: reusing $exe (stamped $sha)" }
            $exitCode = 0
            if ($Run -or $Boot) {
                $report = NewReport
                $log = LogFile 'run'
                $runArgs = Get-PlayerRunArgs (BootLine $report) -Graphics:$Graphics -LogFile $log -CaptureSize @($cfg.lane.captureSize)
                Write-Host "lane: $exe $($runArgs -join ' ')"
                $r = Invoke-Captured $exe $runArgs ($caps.run * 60) $buildDir -Watch (New-RunSupervisor $log $stall @($cfg.lane.logIgnore))
                $exitCode = Close-Run $report $log $(switch ($r.Code) { 124 { 3 } 125 { 4 } default { $r.Code } })
            }
        }
        'editorplay' {
            $report = NewReport
            $log = LogFile 'editorplay'
            $launch = (BootLine $report | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
            Write-Host "lane: UNIDAD_LAUNCH=$launch"
            $code = Invoke-LaneUnity $editor (Get-UnityArgs 'editorplay' $proj $log -Method (Resolve-ExecMethod 'EditorPlay' $cfg.lane.execPrefix)) $owner $caps.run $slotPath $target 'editorplay' $WaitMinutes @{
                UNIDAD_LAUNCH = $launch; UNIDAD_LANE_EDITOR_PLAY = ($report -replace '\\', '/')
            } -Watch (New-RunSupervisor $log 0 @($cfg.lane.logIgnore))   # no stall check: an editor's import phases go silent for minutes
            $exitCode = Close-Run $report $log $code
        }
        'init' { $exitCode = 0 }
    }
} finally {
    if ($proj -and $state) { Write-LaneState $proj $state }
    Exit-SlotLock $repo $slotName
}
exit $exitCode
