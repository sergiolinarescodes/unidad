# The live editor's functions (tools/live.ps1): pure cores the smoke test calls, the machine-wide seat pool and the CLI
# call. Dot-sourced; names no repo, slot or game: those come from tools/repo.json (liveSlots, live, lane.testbedMethod).
#
# Seats: <home>/locks/live/seat<N>.json, at most $LiveSeats on the machine across every repo, apart from the lane's one
# editor lock (U-55): a live editor never takes it and the lane never waits for one. A seat's Pid is the live editor
# (live.ps1's own PID while it opens one), so a seat frees itself when that editor exits; a dead or reused PID is stale
# (EditorLock.ps1's Test-LockStale) and deleted by whoever finds it.

. (Join-Path (Split-Path $PSScriptRoot -Parent) 'lib/Lane.ps1')

$script:LiveSeats = 2
$script:LiveOverlayName = 'com.unidad.live'

# --- pure cores -----------------------------------------------------------------------------------------------------

# What a changed path needs from the open editor. recompile: leave Play, import, compile, play again. replay: import,
# then leave and re-enter Play (boot-time data). style / asset: import in place and stay in Play.
function Get-LiveRoute([string]$Path, [string[]]$ReplayPaths = @()) {
    $p = $Path -replace '\\', '/'
    if ($p -match '\.(cs|asmdef|asmref)$|(^|/)csc\.rsp$|(^|/)(package|manifest)\.json$') { return 'recompile' }
    if (@($ReplayPaths | Where-Object { $_ -and $p -match $_ })) { return 'replay' }
    if ($p -match '\.(uss|tss|uxml)$') { return 'style' }
    'asset'
}

# The strongest route of a sync wins: recompile > replay > style > asset; none when nothing changed.
function Select-LiveRoute([string[]]$Routes) {
    foreach ($r in 'recompile', 'replay', 'style', 'asset') { if (@($Routes) -contains $r) { return $r } }
    'none'
}

# Why a session is over: 'cap' after CapMinutes from its start, 'idle' after IdleMinutes without a live command, else $null.
function Get-LiveExpiry([datetime]$Started, [datetime]$LastTouch, [datetime]$Now, [double]$IdleMinutes, [double]$CapMinutes) {
    if (($Now - $Started).TotalMinutes -ge $CapMinutes) { return 'cap' }
    if (($Now - $LastTouch).TotalMinutes -ge $IdleMinutes) { return 'idle' }
    $null
}

# A tour cut at its first line naming -Label (as a whole word), then the game's hold lines, so Play stays on that label.
function Get-LiveTourCut([string[]]$Lines, [string]$Label, [string[]]$Hold = @()) {
    if (!$Label) { return @($Lines) + @($Hold) }
    $out = [Collections.Generic.List[string]]::new()
    foreach ($l in $Lines) {
        $out.Add($l)
        if ($l -notmatch '^\s*#' -and $l -match "(^|[\s{])$([regex]::Escape($Label))(\s|$|[{}])") { return @($out) + @($Hold) }
    }
    throw "tour has no line naming '$Label'"
}

# The project-relative path of the overlay package and its line in info/exclude.
function Get-LiveOverlayPath($Cfg) {
    $sub = if ($Cfg.projectSubdir -and $Cfg.projectSubdir -ne '.') { "$($Cfg.projectSubdir)/" } else { '' }
    "${sub}Packages/$script:LiveOverlayName"
}

# Copies the overlay source into the slot and lists it in the exclude file once; idempotent. Returns the exclude line.
function Install-LiveOverlay([string]$Source, [string]$SlotRoot, [string]$RelPath, [string]$ExcludeFile) {
    $dst = Join-Path $SlotRoot $RelPath
    if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
    New-Item -ItemType Directory -Force (Split-Path $dst -Parent) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $dst -Recurse -Force
    $line = "/$RelPath/"
    New-Item -ItemType Directory -Force (Split-Path $ExcludeFile -Parent) | Out-Null
    $text = if (Test-Path -LiteralPath $ExcludeFile) { [IO.File]::ReadAllText($ExcludeFile) } else { '' }
    if (($text -split "`r?`n") -notcontains $line) {
        [IO.File]::WriteAllText($ExcludeFile, $text.TrimEnd("`r", "`n") + $(if ($text.Trim()) { "`n" } else { '' }) + "$line`n")
    }
    $line
}

# One manifest line per live frame, in <frames>/frames.jsonl (line 0 the header, "live": true): tools/look scripts read
# a live folder like a player run's, and a verdict check refuses it (no tour or checklist hashes, no round).
function Add-LiveFrame([string]$FramesDir, [Collections.IDictionary]$Header, [Collections.IDictionary]$Entry) {
    $file = Join-Path $FramesDir 'frames.jsonl'
    if (!(Test-Path -LiteralPath $file)) { $Header['live'] = $true; [IO.File]::WriteAllText($file, ($Header | ConvertTo-Json -Compress -Depth 4) + "`n") }
    [IO.File]::AppendAllText($file, ($Entry | ConvertTo-Json -Compress -Depth 4) + "`n")
}

function Get-NextFrameName([string]$FramesDir) {
    $n = @(Get-ChildItem -LiteralPath $FramesDir -Filter '*.png' -ErrorAction SilentlyContinue | ForEach-Object { if ($_.BaseName -match '^\d+$') { [int]$_.BaseName } }) | Measure-Object -Maximum
    '{0:D5}.png' -f $(if ($n.Count) { [int]$n.Maximum + 1 } else { 0 })
}

# --- seats ----------------------------------------------------------------------------------------------------------

function Get-LiveSeatDir([string]$Root = (Join-Path (Get-ToolsHome) 'locks/live')) {
    New-Item -ItemType Directory -Force $Root | Out-Null
    $Root
}

# Live seat records, stale ones deleted. Each carries File and Seat.
function Get-LiveSeats([string]$Dir = (Get-LiveSeatDir), [scriptblock]$IsStale = { param($r) Test-LockStale $r }) {
    @(foreach ($f in Get-ChildItem -LiteralPath $Dir -Filter 'seat*.json' -ErrorAction SilentlyContinue | Sort-Object Name) {
        $r = Read-LockRecord $f.FullName
        if (& $IsStale $r) { Remove-Item -LiteralPath $f.FullName, ($f.FullName -replace '\.json$', '.touch') -Force -ErrorAction SilentlyContinue; continue }
        $r | Add-Member -NotePropertyName File -NotePropertyValue $f.FullName -Force -PassThru
    })
}

# Takes the first free seat for $Record (an ordered hashtable with Pid and ProcessStart); the seat file's path, or $null.
function Enter-LiveSeat($Record, [string]$Dir = (Get-LiveSeatDir), [int]$Max = $script:LiveSeats, [scriptblock]$IsStale = { param($r) Test-LockStale $r }) {
    New-Item -ItemType Directory -Force $Dir | Out-Null
    Get-LiveSeats $Dir $IsStale | Out-Null
    for ($i = 1; $i -le $Max; $i++) {
        $path = Join-Path $Dir "seat$i.json"
        $Record['Seat'] = $i
        try {
            $fs = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
            $bytes = [Text.Encoding]::UTF8.GetBytes(($Record | ConvertTo-Json -Depth 4)); $fs.Write($bytes, 0, $bytes.Length); $fs.Dispose()
            return $path
        } catch [IO.IOException] { }
    }
    $null
}

function Write-LiveSeat($Record) {
    [IO.File]::WriteAllText($Record.File, ($Record | Select-Object * -ExcludeProperty File | ConvertTo-Json -Depth 4))
}

# --- the CLI --------------------------------------------------------------------------------------------------------

# One `unity command` call to the editor with -EditorPid; the parsed JSON answer, or throws with the CLI's text.
function Invoke-LiveCommand([int]$EditorPid, [string[]]$Command, [string[]]$Arguments = @(), [int]$TimeoutSec = 60) {
    # stdout only: the CLI may add a hint line on stderr
    $out = & unity command @Command @Arguments --pid $EditorPid --result-only --timeout $TimeoutSec 2>$null | ForEach-Object { "$_" } | Where-Object { $_ -notmatch '^<claude-code-hint' }
    $text = ($out -join "`n").Trim()
    if ($LASTEXITCODE -ne 0) { throw "unity command $($Command -join ' ') failed ($LASTEXITCODE): $text" }
    $value = try { $text | ConvertFrom-Json } catch { $text }
    while ($value -is [string] -and $value.TrimStart().StartsWith('{')) { $value = $value | ConvertFrom-Json }
    if ($value -isnot [pscustomobject]) { throw "unity command $($Command -join ' ') answered no JSON object: $text" }
    $value
}

# --- the window: out of sight, never focused -------------------------------------------------------------------------
# A minimised editor does not tick (no Play frames, no capture, and its idle watchdog never fires: seen on 7000.0.0a10),
# so the live editor stays restored but parked off-screen at the bottom of the z-order, and keyboard focus goes back to
# whatever window had it before the launch.

function Initialize-LiveWin32 {
    if ('UnidadLiveWin32' -as [type]) { return }
    Add-Type -Namespace '' -Name UnidadLiveWin32 -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct STARTUPINFO {
    public int cb; public string reserved, desktop, title; public int x, y, w, h, xChars, yChars, fill, flags; public short show, reserved2;
    public IntPtr reserved3, stdIn, stdOut, stdErr; }
[StructLayout(LayoutKind.Sequential)] public struct PROCINFO { public IntPtr process, thread; public int pid, tid; }
[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateProcess(string app, System.Text.StringBuilder cmd,
    IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCINFO pi);
[DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
[DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
[DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
[DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
[DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
[DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
[DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
[DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
public delegate bool EnumProc(IntPtr h, IntPtr l);
// No inherited handles (the editor must not hold the caller's pipes open) and SW_SHOWNOACTIVATE for its first window.
public static int Start(string exe, string args, string cwd) {
    var si = new STARTUPINFO(); si.cb = Marshal.SizeOf(si); si.flags = 1; si.show = 4;   // STARTF_USESHOWWINDOW, SW_SHOWNOACTIVATE
    PROCINFO pi;
    if (!CreateProcess(exe, new System.Text.StringBuilder("\"" + exe + "\" " + args), IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, cwd, ref si, out pi))
        throw new System.ComponentModel.Win32Exception();
    CloseHandle(pi.thread); CloseHandle(pi.process);
    return pi.pid;
}
// Parks the process's visible top-level windows; hands the foreground back to userWindow if the editor took it.
public static int Park(uint target, IntPtr userWindow) {
    int n = 0, left = GetSystemMetrics(76) - 4000;   // SM_XVIRTUALSCREEN: 4000 px left of every monitor
    EnumWindows((h, l) => {
        uint pid; GetWindowThreadProcessId(h, out pid);
        if (pid != target || !IsWindowVisible(h)) return true;
        RECT r; GetWindowRect(h, out r);
        if (r.L <= left + 100 && !IsIconic(h) && !IsZoomed(h)) return true;   // already parked
        if (IsIconic(h)) ShowWindow(h, 4);   // SW_SHOWNOACTIVATE
        if (IsZoomed(h)) SetWindowLong(h, -16, GetWindowLong(h, -16) & ~0x01000000);   // drop WS_MAXIMIZE
        SetWindowPos(h, new IntPtr(1), left, 0, 1700, 1000, 0x0030);   // HWND_BOTTOM, SWP_NOACTIVATE|SWP_FRAMECHANGED
        n++; return true; }, IntPtr.Zero);
    uint fgPid; GetWindowThreadProcessId(GetForegroundWindow(), out fgPid);
    if (fgPid == target && userWindow != IntPtr.Zero) SetForegroundWindow(userWindow);
    return n;
}
'@
}

# Starts the editor with no window activation and no inherited handles; returns its Process. Remembers the window that
# had the foreground so Hide-LiveWindows can hand it back.
function Start-LiveEditor([string]$Editor, [string]$ProjectPath, [string]$LogFile, [switch]$Batch) {
    Initialize-LiveWin32
    $script:LiveUserWindow = [UnidadLiveWin32]::GetForegroundWindow()
    $id = [UnidadLiveWin32]::Start($Editor, "$(if ($Batch) { '-batchmode ' })-projectPath `"$ProjectPath`" -logFile `"$LogFile`"", (Split-Path $ProjectPath -Parent))
    Get-Process -Id $id
}

# Parks the editor's windows off-screen (see above); called while the editor loads and enters Play. Returns how many moved.
function Hide-LiveWindows([int]$ProcessId) {
    Initialize-LiveWin32
    $user = if ($script:LiveUserWindow) { $script:LiveUserWindow } else { [IntPtr]::Zero }
    [UnidadLiveWin32]::Park([uint32]$ProcessId, $user)
}
