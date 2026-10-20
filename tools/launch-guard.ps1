# The owner's control over every Unity process the tools start (U-49). Dot-sourced by unity-lane.ps1, testbed.ps1 and
# the loop; testbed.ps1 -StopAll | -Allow | -List are its front door.
#
#   STOP file      <home>/STOP. While it exists no lane mode, testbed or loop lane call starts Unity, in any checkout or
#                  slot. Only a person removes it (-Allow refuses inside an agent session).
#   run registry   <home>/runs/<pid>.json, one RunRecord per process a tool started: PID, launcher PID, kind
#                  (lane | testbed-editor | player), slot, branch, mode, start, lifetime cap, who started it.
#   lifetime       a capped run is killed at its cap and the moment a STOP file appears; on Windows it sits in a job
#                  object that kills it when its launcher dies.
# An unregistered Unity.exe is the user's: it is listed, never killed.
# <home> is %LOCALAPPDATA%/Unidad/guard (UNIDAD_TOOLS_HOME moves the root; UNIDAD_TESTBED_HOME overrides this folder).

$MaxRunSeconds = 1800

function Get-LaunchHome {
    $dir = if ($env:UNIDAD_TESTBED_HOME) { $env:UNIDAD_TESTBED_HOME }
        else { Join-Path $(if ($env:UNIDAD_TOOLS_HOME) { $env:UNIDAD_TOOLS_HOME } else { Join-Path $env:LOCALAPPDATA 'Unidad' }) 'guard' }
    New-Item -ItemType Directory -Force -Path (Join-Path $dir 'runs') | Out-Null
    $dir
}

function Get-StopFile { Join-Path (Get-LaunchHome) 'STOP' }

# ConvertFrom-Json turns ISO strings into DateTime; a DateTime round-tripped through a string parses in the current
# culture (10's Spanish-locale bug). Take a DateTime as is; parse a string invariant.
function ConvertTo-LaunchUtc($value) {
    if ($value -is [DateTime]) { return $value.ToUniversalTime() }
    [DateTime]::Parse([string]$value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
}

if (!(Get-Command Test-AgentSession -ErrorAction SilentlyContinue)) {
    function Test-AgentSession { [bool]($env:CLAUDECODE -or $env:CLAUDE_CODE_ENTRYPOINT -or $env:CODEX_SANDBOX) }
}

function Get-LaunchStarter { if (Test-AgentSession) { 'agent' } else { 'user' } }

function Test-LaunchStopped { Test-Path -LiteralPath (Get-StopFile) }

function Assert-LaunchAllowed {
    $stop = Get-StopFile
    if (!(Test-Path -LiteralPath $stop)) { return }
    $why = (Get-Content -LiteralPath $stop -Raw -ErrorAction SilentlyContinue)
    throw "Unity launches are STOPPED ($stop$(if ($why) { ": $($why.Trim())" })). Nothing starts until the owner runs: pwsh -File tools/testbed.ps1 -Allow"
}

function Get-ProcessTable {
    Get-CimInstance Win32_Process | ForEach-Object {
        [pscustomobject]@{ Id = [int]$_.ProcessId; ParentId = [int]$_.ParentProcessId; Name = "$($_.Name)"; CommandLine = "$($_.CommandLine)" }
    }
}

function Get-RunRecords {
    $runs = Join-Path (Get-LaunchHome) 'runs'
    foreach ($file in Get-ChildItem -LiteralPath $runs -Filter '*.json' -ErrorAction SilentlyContinue) {
        $record = try { Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json } catch { $null }
        if (!$record) { Remove-Item -LiteralPath $file.FullName -Force -ErrorAction SilentlyContinue; continue }
        $process = Get-Process -Id $record.Pid -ErrorAction SilentlyContinue
        $started = try { $process.StartTime.ToUniversalTime() } catch { $null }
        $alive = $process -and (!$record.ProcessStart -or !$started -or [Math]::Abs(($started - (ConvertTo-LaunchUtc $record.ProcessStart)).TotalSeconds) -lt 2)
        if (!$alive) { Remove-Item -LiteralPath $file.FullName -Force -ErrorAction SilentlyContinue; continue }
        $record | Add-Member -NotePropertyName File -NotePropertyValue $file.FullName -PassThru
    }
}

function Register-TrackedRun([Diagnostics.Process]$Process, [string]$Kind, [string]$Root, [string]$Branch, [string]$Mode, [int]$MaxSeconds, [string]$StartedBy) {
    $record = [ordered]@{
        Pid          = $Process.Id
        ProcessStart = "$(try { $Process.StartTime.ToUniversalTime().ToString('o') } catch { '' })"
        LauncherPid  = $PID
        Kind         = $Kind
        Root         = $Root
        Branch       = $Branch
        Mode         = $Mode
        Started      = [DateTime]::UtcNow.ToString('o')
        MaxSeconds   = $MaxSeconds
        StartedBy    = $(if ($StartedBy) { $StartedBy } else { Get-LaunchStarter })
    }
    $file = Join-Path (Get-LaunchHome) "runs/$($Process.Id).json"
    $record | ConvertTo-Json | Set-Content -LiteralPath $file -Encoding utf8
    $file
}

function Unregister-TrackedRun([string]$File) { if ($File) { Remove-Item -LiteralPath $File -Force -ErrorAction SilentlyContinue } }

# A job object that kills its processes when the last handle closes, i.e. when the launcher exits however it exits.
function New-KillOnCloseJob([Diagnostics.Process]$Process) {
    if (!('Unidad.Tools.KillOnCloseJob' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace Unidad.Tools {
public sealed class KillOnCloseJob {
    [StructLayout(LayoutKind.Sequential)] struct Basic { public long A, B; public uint LimitFlags; public UIntPtr C, D; public uint E; public UIntPtr F; public uint G, H; }
    [StructLayout(LayoutKind.Sequential)] struct Io { public ulong A, B, C, D, E, F; }
    [StructLayout(LayoutKind.Sequential)] struct Extended { public Basic Basic; public Io Io; public UIntPtr G, H, I, J; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr a, string name);
    [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int cls, ref Extended info, int size);
    [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    public readonly IntPtr Handle;
    public KillOnCloseJob(IntPtr process) {
        Handle = CreateJobObject(IntPtr.Zero, null);
        var info = new Extended();
        info.Basic.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if (!SetInformationJobObject(Handle, 9, ref info, Marshal.SizeOf(typeof(Extended)))) throw new InvalidOperationException("SetInformationJobObject failed: " + Marshal.GetLastWin32Error());
        if (!AssignProcessToJobObject(Handle, process)) throw new InvalidOperationException("AssignProcessToJobObject failed: " + Marshal.GetLastWin32Error());
    }
}
}
'@
    }
    try { [Unidad.Tools.KillOnCloseJob]::new($Process.Handle) }
    catch { Write-Host "[guard] could not tie PID $($Process.Id) to its launcher ($($_.Exception.Message)); the cap still holds"; $null }
}

# Waits for a run this tool started; kills its tree at the cap or when STOP appears. Returns the exit code, 124 at the
# cap, 125 on STOP.
function Wait-TrackedRun([Diagnostics.Process]$Process, [int]$TimeoutSec) {
    $limit = if ($TimeoutSec -gt 0) { $TimeoutSec } else { $MaxRunSeconds }
    $deadline = [DateTime]::UtcNow.AddSeconds($limit)
    $stop = Get-StopFile
    while (-not $Process.WaitForExit(1000)) {
        $why = $null
        if (Test-Path -LiteralPath $stop) { $why = 'a STOP file appeared'; $code = 125 }
        elseif ([DateTime]::UtcNow -ge $deadline) { $why = "it was still running after $limit s"; $code = 124 }
        if ($why) {
            $Process.Kill($true)
            $Process.WaitForExit()
            Write-Host "[guard] PID $($Process.Id) stopped: $why"
            return $code
        }
    }
    $Process.WaitForExit()
    $Process.ExitCode
}

# -StopAll: writes STOP, then ends every registered run, the launchers of capped runs and the script chains above them
# (unity-lane.ps1, testbed.ps1, loop/run.ps1), never its own ancestors, and never an unregistered Unity.exe.
function Stop-AllLaunches {
    $stop = Get-StopFile
    "stopped $([DateTime]::UtcNow.ToString('u')) by $(Get-LaunchStarter) (PID $PID)" | Set-Content -LiteralPath $stop -Encoding utf8
    Write-Host "[guard] STOP file written: $stop"
    $table = @(Get-ProcessTable)
    $byId = @{}; foreach ($p in $table) { $byId[$p.Id] = $p }
    $protected = [Collections.Generic.HashSet[int]]::new()
    for ($id = $PID; $id -and $byId.ContainsKey($id) -and $protected.Add($id); $id = $byId[$id].ParentId) { }
    $targets = [Collections.Generic.HashSet[int]]::new()
    foreach ($record in Get-RunRecords) {
        [void]$targets.Add([int]$record.Pid)
        if ($record.MaxSeconds) { [void]$targets.Add([int]$record.LauncherPid) }
    }
    foreach ($p in $table) {
        if ($p.Name -notmatch '^Unity' -and $p.CommandLine -match 'unity-lane\.ps1|testbed\.ps1|loop[\\/]run\.ps1') { [void]$targets.Add($p.Id) }
    }
    foreach ($id in @($targets)) {
        $parent = if ($byId.ContainsKey($id)) { $byId[$id].ParentId } else { 0 }
        while ($parent -and $byId.ContainsKey($parent) -and !$protected.Contains($parent)) {
            $p = $byId[$parent]
            $script = $p.Name -match '^(pwsh|powershell|bash|sh|cmd)(\.exe)?$' -and $p.CommandLine -match '(\s-(File|Command|c|EncodedCommand)\b|\s/c\s|\.ps1|\.sh)' -and $p.CommandLine -notmatch '-NoExit'
            if (!$script) { break }
            [void]$targets.Add($parent)
            $parent = $p.ParentId
        }
    }
    $registered = @(Get-RunRecords | ForEach-Object { [int]$_.Pid })
    $stopped = 0
    foreach ($id in $targets) {
        if ($protected.Contains($id)) { continue }
        if ($byId.ContainsKey($id) -and $byId[$id].Name -match '^Unity' -and $registered -notcontains $id) { continue }
        $process = Get-Process -Id $id -ErrorAction SilentlyContinue
        if (!$process) { continue }
        $line = if ($byId.ContainsKey($id)) { $byId[$id].CommandLine } else { $process.ProcessName }
        try { $process.Kill($true); $stopped++; Write-Host "[guard] stopped PID $id  $line" }
        catch { Write-Host "[guard] could not stop PID $id ($($_.Exception.Message))" }
    }
    Get-RunRecords | Out-Null
    Write-Host "[guard] $stopped process(es) stopped. Nothing launches until: pwsh -File tools/testbed.ps1 -Allow"
}

function Enable-Launches {
    if (Test-AgentSession) { throw '-Allow is the owner''s: an agent session cannot lift a STOP. Ask the owner to run it.' }
    $stop = Get-StopFile
    if (Test-Path -LiteralPath $stop) { Remove-Item -LiteralPath $stop -Force; Write-Host "[guard] launches allowed again (removed $stop)" }
    else { Write-Host '[guard] launches were not stopped' }
}

function Show-Launches {
    $stop = Get-StopFile
    if (Test-Path -LiteralPath $stop) { Write-Host "[guard] STOPPED: $((Get-Content -LiteralPath $stop -Raw).Trim())  (lift it with -Allow)" }
    else { Write-Host '[guard] launches allowed (stop everything with -StopAll)' }
    $records = @(Get-RunRecords)
    if ($records.Count -eq 0) { Write-Host '[guard] no registered runs' }
    foreach ($r in $records) {
        $age = [int]([DateTime]::UtcNow - (ConvertTo-LaunchUtc $r.Started)).TotalSeconds
        Write-Host ("[guard] PID {0}  {1} by {2}  {3} s old (cap {4})  {5}  branch {6}  mode {7}  launcher PID {8}" -f $r.Pid, $r.Kind, $r.StartedBy, $age, $(if ($r.MaxSeconds) { "$($r.MaxSeconds) s" } else { 'none: the user''s window' }), $r.Root, $r.Branch, $r.Mode, $r.LauncherPid)
    }
    $known = @($records | ForEach-Object { [int]$_.Pid })
    foreach ($p in Get-ProcessTable | Where-Object { $_.Name -eq 'Unity.exe' -and $_.Id -notin $known -and $_.CommandLine -notmatch 'AssetImportWorker' }) {
        Write-Host "[guard] user's editor PID $($p.Id) (left alone)  $($p.CommandLine)"
    }
}
