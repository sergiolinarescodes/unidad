# The machine-wide editor lock (U-55): at most one agent-started Unity.exe on this machine, across every repo.
#   . tools/lib/EditorLock.ps1
#   Invoke-WithEditorLock -Owner <who> -TimeoutMinutes <hold cap> [-WaitMinutes 60] -ScriptBlock { ... }
# Lock file <home>/locks/unity-editor.lock; FIFO tickets <home>/locks/queue/<utcTicks:D19>-<pid>.json. Re-entrant for
# the same -Owner (a lane call that runs sandbox-gate inside never deadlocks): the inner call runs at once and leaves
# the lock to the outer one. A lock or ticket whose PID is dead, or was reused by another program, is stale and deleted
# by whoever finds it. Waiting longer than -WaitMinutes throws EditorLockTimeout (callers exit 3). The hold cap is
# recorded in the lock for -Status and enforced by the caller, which kills only the Unity PID it started (U-49).
# The user's interactive editors never take this lock and are never counted.

. (Join-Path $PSScriptRoot 'UnidadTools.ps1')

function Get-EditorLockDir {
    $dir = Join-Path (Get-ToolsHome) 'locks'
    New-Item -ItemType Directory -Force -Path (Join-Path $dir 'queue') | Out-Null
    $dir
}

function Get-ProcessStartUtc([int]$ProcessId) {
    $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if (!$p) { return $null }
    try { $p.StartTime.ToUniversalTime() } catch { $null }
}

function ConvertTo-LockUtc($Value) {
    if ($Value -is [DateTime]) { return $Value.ToUniversalTime() }
    [DateTime]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
}

# Stale: no record, a dead PID, or a live PID whose start time differs by 2 s or more (PID reuse).
function Test-LockStale($Record, [scriptblock]$GetProcessStart = { param($id) Get-ProcessStartUtc $id }) {
    if (!$Record -or !$Record.Pid) { return $true }
    $started = & $GetProcessStart ([int]$Record.Pid)
    if (!$started) { return $true }
    if ($Record.ProcessStart -and [Math]::Abs(($started - (ConvertTo-LockUtc $Record.ProcessStart)).TotalSeconds) -ge 2) { return $true }
    $false
}

# The oldest live ticket (names sort by UTC ticks).
function Get-QueueHead([object[]]$Tickets, [scriptblock]$IsStale) {
    foreach ($t in @($Tickets | Sort-Object Name)) { if (!(& $IsStale $t)) { return $t } }
    $null
}

function Read-LockRecord([string]$Path) {
    try { Get-Content -Raw -LiteralPath $Path -ErrorAction Stop | ConvertFrom-Json } catch { $null }
}

function New-LockRecord([string]$Owner, [int]$TimeoutMinutes) {
    $now = [DateTime]::UtcNow
    [ordered]@{
        Pid = $PID; ProcessStart = (Get-ProcessStartUtc $PID).ToString('o'); Owner = $Owner
        Cwd = (Get-Location).Path -replace '\\', '/'; Started = $now.ToString('o'); CapMinutes = $TimeoutMinutes
    }
}

function Show-EditorLockQueue([string]$Dir) {
    $holder = Read-LockRecord (Join-Path $Dir 'unity-editor.lock')
    if ($holder) { Write-Host "[editor-lock] held by $($holder.Owner) (pid $($holder.Pid), since $($holder.Started), cap $($holder.CapMinutes) min)" }
    foreach ($t in Get-ChildItem -LiteralPath (Join-Path $Dir 'queue') -Filter '*.json' | Sort-Object Name) {
        $r = Read-LockRecord $t.FullName
        Write-Host "[editor-lock]   waiting: $($r.Owner) (pid $($r.Pid), since $($r.Started))"
    }
}

function Invoke-WithEditorLock {
    param(
        [Parameter(Mandatory)][scriptblock]$ScriptBlock,
        [string]$Owner = $(if ($env:UNIDAD_EDITOR_LOCK_OWNER) { $env:UNIDAD_EDITOR_LOCK_OWNER } else { "pid$PID" }),
        [int]$TimeoutMinutes = 15,
        [double]$WaitMinutes = 60
    )
    $dir = Get-EditorLockDir
    $lock = Join-Path $dir 'unity-editor.lock'
    $current = Read-LockRecord $lock
    if ($current -and $current.Owner -eq $Owner -and !(Test-LockStale $current)) { return & $ScriptBlock }

    $record = New-LockRecord $Owner $TimeoutMinutes
    $ticket = Join-Path $dir ("queue/{0:D19}-{1}.json" -f [DateTime]::UtcNow.Ticks, $PID)
    $record | ConvertTo-Json | Set-Content -LiteralPath $ticket
    $deadline = [DateTime]::UtcNow.AddMinutes($WaitMinutes)
    $nextPrint = [DateTime]::UtcNow
    $held = $false
    try {
        while ($true) {
            $tickets = @(Get-ChildItem -LiteralPath (Join-Path $dir 'queue') -Filter '*.json')
            $head = Get-QueueHead $tickets {
                param($t)
                $stale = Test-LockStale (Read-LockRecord $t.FullName)
                if ($stale) { Remove-Item -LiteralPath $t.FullName -Force -ErrorAction SilentlyContinue }
                $stale
            }
            if ($head -and $head.FullName -eq (Get-Item -LiteralPath $ticket).FullName) {
                if ((Test-Path -LiteralPath $lock) -and (Test-LockStale (Read-LockRecord $lock))) {
                    Remove-Item -LiteralPath $lock -Force -ErrorAction SilentlyContinue
                }
                try {
                    $fs = [IO.File]::Open($lock, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
                    $bytes = [Text.Encoding]::UTF8.GetBytes(($record | ConvertTo-Json))
                    $fs.Write($bytes, 0, $bytes.Length); $fs.Dispose()
                    $held = $true
                } catch [IO.IOException] { }
            }
            if ($held) { break }
            if ([DateTime]::UtcNow -ge $deadline) { throw [TimeoutException]::new("EditorLockTimeout: waited $WaitMinutes min for the editor lock") }
            if ([DateTime]::UtcNow -ge $nextPrint) { Show-EditorLockQueue $dir; $nextPrint = [DateTime]::UtcNow.AddSeconds(30) }
            Start-Sleep -Milliseconds 2000
        }
        Remove-Item -LiteralPath $ticket -Force -ErrorAction SilentlyContinue
        $prevOwner = $env:UNIDAD_EDITOR_LOCK_OWNER
        $env:UNIDAD_EDITOR_LOCK_OWNER = $Owner
        try { & $ScriptBlock } finally { $env:UNIDAD_EDITOR_LOCK_OWNER = $prevOwner }
    } finally {
        Remove-Item -LiteralPath $ticket -Force -ErrorAction SilentlyContinue
        if ($held) {
            $mine = Read-LockRecord $lock
            if ($mine -and $mine.Pid -eq $PID -and $mine.Owner -eq $Owner) { Remove-Item -LiteralPath $lock -Force -ErrorAction SilentlyContinue }
        }
    }
}

# Waits for a process this script started; at the cap kills that PID's tree only (never another process). Returns
# the exit code, or 124 when killed at the cap.
function Wait-OwnProcess([Diagnostics.Process]$Process, [int]$CapMinutes) {
    if ($Process.WaitForExit($CapMinutes * 60 * 1000)) { $Process.WaitForExit(); return $Process.ExitCode }
    Write-Warning "pid $($Process.Id) passed its $CapMinutes min cap; killing that process tree only"
    $Process.Kill($true)
    124
}
