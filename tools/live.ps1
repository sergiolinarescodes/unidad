<#
Live iteration (docs/LIVE.md): one Unity editor stays open in Play mode on your branch in a live slot; you edit in your
code slot, -Sync, and read a fresh frame in seconds without closing it. The inner loop only: a live frame is never a
look verdict (the player-build look is).
  live.ps1 -Open   [-Branch <b>] [-From <code slot>] [-Start <t>] [-Set "k=v;..."]   take a seat, prepare the slot, play
  live.ps1 -Sync   [-Branch <b>] [-From <code slot>]     copy what changed and route it (import in place | replay | recompile)
  live.ps1 -Replay [-Branch <b>]                          leave and re-enter Play with the same launch line (and last tour)
  live.ps1 -Shot   [-Branch <b>] -Label <l> -Out <dir>    one labelled PNG of the game view into <dir>/frames
  live.ps1 -Tour <file> [-Until <label>] -Out <dir>       play a tour through repo.json live.tour (the game's frames)
  live.ps1 -Eval "<C# expression or statements>"          run code in the open editor (Play mode included)
  live.ps1 -Query                                         visible UI Toolkit elements with text/name and rects
  live.ps1 -Close [-Branch <b>]  /  -Status
-Branch defaults to the current directory's branch, -From to its checkout. Seats: at most 2 live editors on the machine,
apart from the lane's editor lock; an editor closes itself after live.idleMinutes without a live command or
live.capMinutes in all. Exit: 0 ok; 1 a command failed (the editor stays open); 2 usage or environment; 3 no seat or no
session; 4 STOP.
#>
param(
    [switch]$Open, [switch]$Sync, [switch]$Replay, [switch]$Shot, [string]$Tour, [string]$Eval, [switch]$Query,
    [switch]$Close, [switch]$Status,
    [string]$Branch, [string]$From, [string]$Start, [string]$Set, [string]$Label, [string]$Until, [string]$Out,
    [double]$IdleMinutes, [double]$CapMinutes, [double]$WaitMinutes = 5, [string]$UnityPath
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'live/Live.ps1')

$verbs = @(@($Open, $Sync, $Replay, $Shot, [bool]$Tour, [bool]$Eval, $Query, $Close, $Status) | Where-Object { $_ })
if ($verbs.Count -ne 1) { Write-Host 'usage: live.ps1 -Open|-Sync|-Replay|-Shot|-Tour <f>|-Eval <code>|-Query|-Close|-Status [-Branch b] (see the header)'; exit 2 }

$main = Get-MainCheckout $PSScriptRoot
$cfg = Get-RepoConfig (Get-RepoRoot $PSScriptRoot)
$live = $cfg.live
$repo = $cfg.name
$sw = [Diagnostics.Stopwatch]::StartNew()
function Say([string]$Text) { Write-Host ("live: {0} ({1} ms)" -f $Text, $sw.ElapsedMilliseconds) }

if ($Status) {
    $now = [DateTime]::UtcNow
    $seats = @(Get-LiveSeats)
    Write-Host "live: $($seats.Count) of $script:LiveSeats seats taken on this machine"
    foreach ($s in $seats) {
        $touch = if ($s.Touch -and (Test-Path -LiteralPath $s.Touch)) { (Get-Item -LiteralPath $s.Touch).LastWriteTimeUtc } else { ConvertTo-LockUtc $s.Started }
        $started = ConvertTo-LockUtc $s.Started
        Write-Host ("live: seat {0}  {1}/{2}  {3}  editor PID {4}  up {5:0.0} min  idle {6:0.0} of {7} min  cap {8} min{9}" -f $s.Seat, $s.Repo, $s.Branch, $s.Slot, $s.Pid,
            ($now - $started).TotalMinutes, ($now - $touch).TotalMinutes, $s.IdleMinutes, $s.CapMinutes,
            $(if ($e = Get-LiveExpiry $started $touch $now $s.IdleMinutes $s.CapMinutes) { "  (past its $e limit: closing itself)" }))
    }
    exit 0
}

if (!$From) { $From = Get-RepoRoot (Get-Location).Path }
$From = $From -replace '\\', '/'
if (!$Branch) { $Branch = (git -C $From branch --show-current).Trim() }
if (!$Branch) { Write-Host 'live: -Branch is required (the current directory is on no branch)'; exit 2 }
$seat = Get-LiveSeats | Where-Object { $_.Repo -eq $repo -and $_.Branch -eq $Branch } | Select-Object -First 1

function Touch-Seat($S) { if ($S.Touch) { [IO.File]::WriteAllText($S.Touch, [DateTime]::UtcNow.ToString('o')) } }
function Live([string[]]$Command, [string[]]$Arguments = @(), [int]$TimeoutSec = 60) { Invoke-LiveCommand $seat.Pid $Command $Arguments $TimeoutSec }
function Get-LiveStatus { try { Live @('live', 'status') @() 15 } catch { $null } }
# Polls live status until $Test passes; the last status, or throws after $Seconds.
function Wait-Live([scriptblock]$Test, [int]$Seconds, [string]$What) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ($true) {
        if (!(Get-Process -Id $seat.Pid -ErrorAction SilentlyContinue)) { throw "the live editor (PID $($seat.Pid)) exited while waiting for $What; log $($seat.Log)" }
        $s = Get-LiveStatus
        if ($s -and (& $Test $s)) { return $s }
        if ([DateTime]::UtcNow -ge $deadline) { throw "no $What after $Seconds s (last status: $($s | ConvertTo-Json -Compress)); log $($seat.Log)" }
        Start-Sleep -Milliseconds 500
    }
}
function Wait-Ready([int]$Seconds = 300) {
    Wait-Live { param($s) $s.playing -and !$s.pending -and $s.playSeconds -ge $live.settleSeconds } $Seconds 'Play mode'
}
function Enter-Play([string]$Launch) {
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Launch))
    Live @('live', 'play') @('--launch64', $b64) | Out-Null
    Wait-Ready | Out-Null
}
function Get-LaunchFor($S) { if ($S.TourLaunch) { $S.TourLaunch } else { $S.Launch } }

# Copies every file that differs between the code slot and the live slot (changes against the slot's commit, untracked
# files, and anything synced before, so a revert syncs too). Returns @{ Path; Route } per copied or deleted file.
function Copy-Changes([string]$Slot) {
    $paths = @(git -C $From -c core.quotepath=off diff --name-only $seat.Sha) + @(git -C $From -c core.quotepath=off ls-files --others --exclude-standard) + @($seat.Synced)
    $overlay = Get-LiveOverlayPath $cfg
    @(foreach ($p in $paths | Where-Object { $_ } | Sort-Object -Unique) {
        if ($p.StartsWith("$overlay/")) { continue }
        $src = Join-Path $From $p; $dst = Join-Path $Slot $p
        if (Test-Path -LiteralPath $src -PathType Leaf) {
            if ((Test-Path -LiteralPath $dst) -and (Get-FileHash -LiteralPath $src).Hash -eq (Get-FileHash -LiteralPath $dst).Hash) { continue }
            New-Item -ItemType Directory -Force (Split-Path $dst -Parent) | Out-Null
            Copy-Item -LiteralPath $src $dst -Force
            (Get-Item -LiteralPath $dst).LastWriteTimeUtc = [DateTime]::UtcNow
        } elseif (Test-Path -LiteralPath $dst -PathType Leaf) { Remove-Item -LiteralPath $dst -Force }
        else { continue }
        @{ Path = $p; Route = Get-LiveRoute $p @($live.replayPaths) }
    })
}
function Invoke-Prepare([string]$Slot) {
    if ($cfg.lane.prepare -and (Test-Path -LiteralPath (Join-Path $Slot $cfg.lane.prepare))) {
        Push-Location $Slot
        try { & pwsh -NoProfile -File $cfg.lane.prepare | Out-Null } finally { Pop-Location }
    }
}
function Save-Seat { Write-LiveSeat $script:seat }

# --- verbs that need a session ----------------------------------------------------------------------------------------

if (!$Open) {
    if (!$seat) { Write-Host "live: no live session for $repo/$Branch (open one: pwsh -File tools/live.ps1 -Open -Branch $Branch)"; exit 3 }
    Touch-Seat $seat
    try {
        if ($Close) {
            Live @('live', 'quit') | Out-Null
            $p = Get-Process -Id $seat.Pid -ErrorAction SilentlyContinue
            if ($p -and !$p.WaitForExit(120000)) { Write-Host "live: editor PID $($seat.Pid) did not exit in 120 s; it closes itself at its idle limit"; exit 1 }
            Remove-Item -LiteralPath $seat.File, $seat.Touch -Force -ErrorAction SilentlyContinue
            Say "closed $Branch (seat $($seat.Seat) free)"
        } elseif ($Sync) {
            $changes = @(Copy-Changes $seat.SlotPath)
            $seat.Synced = @(@($seat.Synced) + @($changes | ForEach-Object { $_.Path }) | Where-Object { $_ } | Sort-Object -Unique)
            Save-Seat
            if (!$changes) { Say 'nothing changed'; exit 0 }
            Invoke-Prepare $seat.SlotPath
            $route = Select-LiveRoute @($changes | ForEach-Object { $_.Route })
            $changes | ForEach-Object { Write-Host "live:   $($_.Route)  $($_.Path)" }
            Say "copied $($changes.Count) file(s); route $route"
            if ($route -eq 'recompile') {
                Live @('live', 'stop') | Out-Null
                Wait-Live { param($s) !$s.playing } 60 'Edit mode' | Out-Null
                Live @('live', 'refresh') @() 600 | Out-Null
                $s = Wait-Live { param($s) !$s.compiling -and !$s.updating } 600 'a finished compile'
                if ($s.compileFailed) {
                    Get-CompileErrors @(Get-Content -LiteralPath $seat.Log -Tail 400) | ForEach-Object { Write-Host "live: $_" }
                    Say 'compile RED: fix and -Sync again (the editor stays open, in Edit mode)'; exit 1
                }
                Say 'compiled'
                Enter-Play (Get-LaunchFor $seat)
                Say 'recompiled and back in Play'
            } else {
                Live @('live', 'refresh') @() 600 | Out-Null
                Wait-Live { param($s) !$s.compiling -and !$s.updating } 300 'a finished import' | Out-Null
                Say "imported in place ($route)"
                if ($route -eq 'replay') { Enter-Play (Get-LaunchFor $seat); Say 'replayed' }
            }
        } elseif ($Replay) {
            Enter-Play (Get-LaunchFor $seat)
            Say 'replayed'
        } elseif ($Shot) {
            if (!$Label -or !$Out) { Write-Host 'live: -Shot needs -Label and -Out'; exit 2 }
            $frames = Join-Path ([IO.Path]::GetFullPath($Out)) 'frames'
            New-Item -ItemType Directory -Force $frames | Out-Null
            $name = Get-NextFrameName $frames
            $path = (Join-Path $frames $name) -replace '\\', '/'
            $r = Live @('live', 'shot') @('--path', $path)
            Add-LiveFrame $frames ([ordered]@{ branch = $Branch; commit = $seat.Sha; round = $null; tourSha256 = $null; checklistSha256 = $null }) ([ordered]@{
                frame = $name; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLower(); t = $r.t; unityFrame = $r.unityFrame; label = $Label; live = $true })
            if ($r.targetWidth -ne $r.width -or $r.targetHeight -ne $r.height) { Write-Host "live: warning: the game view renders $($r.targetWidth)x$($r.targetHeight), scaled to $($r.width)x$($r.height)" }
            Say "$path  [$Label]  t=$($r.t) frame=$($r.unityFrame) (capture $($r.ms) ms)"
        } elseif ($Tour) {
            if (!$live.tour -or !$live.tour.set) { Write-Host 'live: this game has no live.tour in tools/repo.json (the launch keys that run a tour); use -Shot'; exit 2 }
            if (!$Out) { Write-Host 'live: -Tour needs -Out'; exit 2 }
            $Out = [IO.Path]::GetFullPath($Out) -replace '\\', '/'
            $frames = "$Out/frames"
            if (Test-Path -LiteralPath $frames) { Remove-Item -LiteralPath $frames -Recurse -Force }
            New-Item -ItemType Directory -Force $frames | Out-Null
            $cut = Get-LiveTourCut @(Get-Content -LiteralPath $Tour) $Until $(if ($Until) { @($live.tour.hold) } else { @() })
            $file = "$Out/live-tour.txt"
            [IO.File]::WriteAllLines($file, [string[]]$cut)
            $set = (@(Split-SetString $Set) + @(Split-SetString ($live.tour.set.Replace('{tour}', $file).Replace('{frames}', $frames)))) -join ';'
            $launch = (ConvertTo-LaunchLine -Start $(if ($Start) { $Start } else { $seat.Start }) -Set $set | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
            $seat.TourLaunch = if ($Until) { $launch } else { $null }
            Save-Seat
            Enter-Play $launch
            $manifest = "$frames/frames.jsonl"
            $seen = { (Test-Path -LiteralPath $manifest) -and (Select-String -LiteralPath $manifest -SimpleMatch "`"label`":`"$Until`"" -Quiet) }
            $deadline = [DateTime]::UtcNow.AddMinutes(10)
            while ([DateTime]::UtcNow -lt $deadline) {
                Touch-Seat $seat
                if ($Until -and (& $seen)) { break }
                $st = Get-LiveStatus
                if ($st -and !$st.playing -and !$st.pending) { break }
                Start-Sleep -Seconds 1
            }
            if (Test-Path -LiteralPath $manifest) {
                $lines = [IO.File]::ReadAllLines($manifest)
                $head = $lines[0] | ConvertFrom-Json -AsHashtable; $head['live'] = $true
                $lines[0] = $head | ConvertTo-Json -Compress -Depth 4
                [IO.File]::WriteAllLines($manifest, $lines)
            }
            $count = @(Get-ChildItem -LiteralPath $frames -File -Include '*.png', '*.jpg' -Recurse).Count
            if (!$Until) { Enter-Play $seat.Launch }
            Say "tour $(Split-Path $Tour -Leaf)$(if ($Until) { " held at $Until" }): $count frame(s) in $frames"
        } elseif ($Eval) {
            $r = Invoke-LiveCommand $seat.Pid @('script', 'eval') @('--code', $Eval) 120
            $r | ConvertTo-Json -Depth 6 | Write-Host
            Say 'eval done. Anything you keep goes into the source, then -Sync: an eval dies with this Play session'
        } elseif ($Query) {
            (Live @('live', 'query')).elements | Format-Table -AutoSize document, name, type, text, x, y, w, h | Out-String -Width 220 | Write-Host
        }
        exit 0
    } catch { Write-Host "live: $($_.Exception.Message)"; exit 1 }
}

# --- -Open ------------------------------------------------------------------------------------------------------------

if ($seat) {
    Touch-Seat $seat
    Write-Host "live: $repo/$Branch is already open: seat $($seat.Seat), editor PID $($seat.Pid), $($seat.SlotPath)"
    exit 0
}
if (Test-LaunchStopped) { Write-Host "live: launches are STOPPED ($(Get-StopFile))"; exit 4 }
$commit = Resolve-LaneCommit $main $Branch
if (!$commit) { Write-Host "live: no local or origin branch '$Branch'"; exit 2 }
$idle = if ($IdleMinutes -gt 0) { $IdleMinutes } else { [double]$live.idleMinutes }
$cap = if ($CapMinutes -gt 0) { $CapMinutes } else { [double]$live.capMinutes }

$record = [ordered]@{
    Pid = $PID; ProcessStart = (Get-ProcessStartUtc $PID).ToString('o'); Repo = $repo; Branch = $Branch; Sha = $commit.Sha
    Slot = $null; SlotPath = $null; Start = $Start; Launch = $null; TourLaunch = $null; Touch = $null; Log = $null
    IdleMinutes = $idle; CapMinutes = $cap; Started = [DateTime]::UtcNow.ToString('o'); Synced = @()
}
$deadline = [DateTime]::UtcNow.AddMinutes($WaitMinutes)
$nextPrint = [DateTime]::UtcNow
while (!($file = Enter-LiveSeat $record)) {
    if ([DateTime]::UtcNow -ge $deadline) {
        Write-Host "live: all $script:LiveSeats live seats stayed taken for $WaitMinutes min (pwsh -File tools/live.ps1 -Status). Fall back to the normal look run (tools/unity-lane.ps1 / look.ps1)."
        exit 3
    }
    if ([DateTime]::UtcNow -ge $nextPrint) {
        Get-LiveSeats | ForEach-Object { Write-Host "live: waiting for a seat; seat $($_.Seat) is $($_.Repo)/$($_.Branch) (PID $($_.Pid))" }
        $nextPrint = [DateTime]::UtcNow.AddSeconds(30)
    }
    Start-Sleep -Seconds 2
}
$seat = (Get-LiveSeats | Where-Object { $_.File -eq $file })
$handedOver = $false
try {
    $taken = @(Get-LiveSeats | Where-Object { $_.Repo -eq $repo -and $_.Slot } | ForEach-Object { $_.Slot })
    $slotName = @($cfg.liveSlots | Where-Object { $taken -notcontains $_ })[0]
    if (!$slotName) { Write-Host "live: every live slot of $repo ($($cfg.liveSlots -join ', ')) is in use"; exit 3 }
    $slotPath = Get-SlotPath $cfg (Split-Path $main -Parent) $slotName
    if ($slotPath -match '--') { Write-Host "live: $slotPath contains '--'"; exit 2 }
    if (!(Test-Path -LiteralPath (Join-Path $slotPath '.git'))) {
        git -C $main worktree prune
        git -C $main worktree add -q --detach $slotPath $commit.Sha
        if ($LASTEXITCODE -ne 0) { Write-Host "live: could not create $slotPath"; exit 2 }
    }
    $proj = Get-UnityProjectPath $cfg $slotPath
    if (Test-ProjectOpen $proj) { Write-Host "live: $proj is open in another editor"; exit 2 }
    $overlay = Get-LiveOverlayPath $cfg
    $lock = "$(if ($cfg.projectSubdir -and $cfg.projectSubdir -ne '.') { "$($cfg.projectSubdir)/" })Packages/packages-lock.json"
    git -C $slotPath update-index --no-skip-worktree -- $lock 2>$null
    git -C $slotPath checkout -q --force --detach $commit.Sha
    if ($LASTEXITCODE -ne 0) { Write-Host "live: checkout of $($commit.Sha) failed in $slotPath"; exit 2 }
    git -C $slotPath clean -qfd
    $seat.Slot = $slotName; $seat.SlotPath = $slotPath; Save-Seat
    $changes = @(Copy-Changes $slotPath)
    $seat.Synced = @($changes | ForEach-Object { $_.Path })
    Invoke-Prepare $slotPath
    $exclude = git -C $slotPath rev-parse --path-format=absolute --git-path info/exclude
    Install-LiveOverlay (Join-Path $PSScriptRoot 'live/editor') $slotPath $overlay $exclude.Trim() | Out-Null
    git -C $slotPath update-index --skip-worktree -- $lock 2>$null   # the overlay's dependencies rewrite the lock file
    Say "slot $slotPath at $($commit.Sha.Substring(0, 7)) ($($commit.Source)), $($changes.Count) uncommitted file(s) from $From, overlay $overlay"

    if (!(Test-Path -LiteralPath (Join-Path $proj 'Library'))) {
        $src = @(@($cfg.unitySlots) + $cfg.loopSlot + $cfg.testbedSlot | Where-Object { $_ } | Where-Object {
                $l = Read-LockRecord (Get-SlotLockPath $repo $_); !$l -or (Test-LockStale $l) } |
            ForEach-Object { Get-UnityProjectPath $cfg (Get-SlotPath $cfg (Split-Path $main -Parent) $_) } |
            Where-Object { Test-Path -LiteralPath (Join-Path $_ 'Library') }) | Select-Object -First 1
        if ($src) {
            Say "seeding Library from $src"
            robocopy "$src/Library" "$proj/Library" /MIR /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
            if ($LASTEXITCODE -ge 8) { Write-Host "live: robocopy failed ($LASTEXITCODE)"; exit 2 }
            Remove-Item -LiteralPath "$proj/Library/.lane-state.json" -Force -ErrorAction SilentlyContinue
        } else { Say 'no lane slot Library to seed from: the first open is a full import' }
    }

    $editor = try { Resolve-UnityEditor $proj $UnityPath $cfg.editorVersion } catch { Write-Host "live: $($_.Exception.Message)"; exit 2 }
    $launch = (ConvertTo-LaunchLine -Start $Start -Set $Set | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    $touch = ($file -replace '\.json$', '.touch')
    [IO.File]::WriteAllText($touch, [DateTime]::UtcNow.ToString('o'))
    New-Item -ItemType Directory -Force "$proj/Logs" | Out-Null
    $log = "$proj/Logs/live-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')).log"
    $psi = [Diagnostics.ProcessStartInfo]::new($editor)
    foreach ($a in '-projectPath', $proj, '-logFile', $log) { $psi.ArgumentList.Add($a) }
    $envs = @{ UNIDAD_LAUNCH = $launch; UNIDAD_LIVE_DOOR = $cfg.lane.testbedMethod; UNIDAD_LIVE_TOUCH = $touch; UNIDAD_LIVE_SIZE = $live.size
        UNIDAD_LIVE_IDLE_SECONDS = "$([int]($idle * 60))"; UNIDAD_LIVE_CAP_SECONDS = "$([int]($cap * 60))" }
    foreach ($k in $envs.Keys) { $psi.Environment[$k] = $envs[$k] }
    $psi.UseShellExecute = $false
    $p = [Diagnostics.Process]::Start($psi)
    Register-TrackedRun $p 'live' $slotPath $Branch 'live' ([int]($cap * 60) + 300) 'agent' | Out-Null
    $seat.Pid = $p.Id; $seat.ProcessStart = $p.StartTime.ToUniversalTime().ToString('o')
    $seat.Launch = $launch; $seat.Touch = $touch; $seat.Log = $log
    Save-Seat
    $handedOver = $true
    Say "editor PID $($p.Id) starting (log $log)"
    Wait-Live { param($s) !$s.compiling -and !$s.updating } 1800 'a running editor' | Out-Null
    Say 'editor answering'
    Enter-Play $launch
    $s = Get-LiveStatus
    Say "OPEN $repo/$Branch  seat $($seat.Seat)  PID $($p.Id)  $slotPath  game view $($s.width)x$($s.height)  idle $idle min / cap $cap min"
    exit 0
} catch {
    Write-Host "live: $($_.Exception.Message)"
    exit 1
} finally {
    if (!$handedOver) { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue }
}
