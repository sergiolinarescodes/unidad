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
# then leave and re-enter Play (boot-time data). style / asset: import in place and stay in Play. none: Unity cannot see
# the path (outside every Assets, Packages and ProjectSettings folder: tools, docs), so it is copied and not imported.
function Get-LiveRoute([string]$Path, [string[]]$ReplayPaths = @()) {
    $p = $Path -replace '\\', '/'
    if ($p -notmatch '(^|/)(Assets|Packages|ProjectSettings)/') { return 'none' }
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

# The labels a tour emits: the first token after shot|burst|act on a non-comment line.
function Get-LiveTourLabels([string[]]$Lines) {
    @($Lines | Where-Object { $_ -notmatch '^\s*#' } | ForEach-Object { if ($_ -match '^\s*(shot|burst|act)\s+([^\s{}]+)') { $Matches[2] } })
}

# The tour lines up to the first line that emits <Label> (exact, via shot|burst|act), then the hold lines. Throws,
# naming the labels the tour does have, when it never emits it.
function Get-LiveTourCut([string[]]$Lines, [string]$Label, [string[]]$Hold = @()) {
    if (!$Label) { return @($Lines) + @($Hold) }
    $out = [Collections.Generic.List[string]]::new()
    foreach ($l in $Lines) {
        $out.Add($l)
        if ($l -notmatch '^\s*#' -and $l -match '^\s*(shot|burst|act)\s+([^\s{}]+)' -and $Matches[2] -eq $Label) { return @($out) + @($Hold) }
    }
    throw "tour never emits label '$Label' (labels: $((Get-LiveTourLabels $Lines) -join ', '))"
}

# The lines of a file the game may still hold open for writing.
function Read-LiveSharedLines([string]$Path) {
    $fs = [IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    try { $r = [IO.StreamReader]::new($fs); @(while (($l = $r.ReadLine()) -ne $null) { $l }) } finally { $fs.Dispose() }
}

# Copies the game's manifest to <Dest> with the header marked "live": true; the game's own file is never written.
function Write-LiveManifestCopy([string]$Source, [string]$Dest) {
    $lines = @(Read-LiveSharedLines $Source)
    if (!$lines.Count) { return }
    $head = $lines[0] | ConvertFrom-Json -AsHashtable; $head['live'] = $true
    $lines[0] = $head | ConvertTo-Json -Compress -Depth 4
    [IO.File]::WriteAllLines($Dest, [string[]]$lines)
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

# --- method reload (live.methodReload, docs/LIVE.md) ----------------------------------------------------------------

# The C# text with comments, string and char literals and preprocessor lines blanked to spaces (newlines and length
# kept), so braces can be counted. shortcut: one level of quotes inside an interpolation hole; deeper nesting misreads
# the braces, which makes the diff fall back to a recompile.
function ConvertTo-LiveCodeMask([string]$Text) {
    $c = $Text.ToCharArray(); $n = $c.Length; $i = 0; $lineStart = $true
    $blank = { param($From, $To) for ($k = $From; $k -lt $To -and $k -lt $n; $k++) { if ($c[$k] -ne "`n") { $c[$k] = ' ' } } }
    $eol = { param($From) $e = $Text.IndexOf("`n", $From); if ($e -lt 0) { $n } else { $e } }
    while ($i -lt $n) {
        $ch = $Text[$i]; $nx = if ($i + 1 -lt $n) { $Text[$i + 1] } else { [char]0 }
        if ($ch -eq "`n") { $lineStart = $true; $i++; continue }
        if ($lineStart -and $ch -eq '#') { $e = & $eol $i; & $blank $i $e; $i = $e; continue }
        if (![char]::IsWhiteSpace($ch)) { $lineStart = $false }
        if ($ch -eq '/' -and $nx -eq '/') { $e = & $eol $i; & $blank $i $e; $i = $e; continue }
        if ($ch -eq '/' -and $nx -eq '*') { $e = $Text.IndexOf('*/', $i + 2); $e = if ($e -lt 0) { $n } else { $e + 2 }; & $blank $i $e; $i = $e; continue }
        if ($ch -eq "'") {
            $j = $i + 1
            while ($j -lt $n -and $Text[$j] -ne "'" -and $Text[$j] -ne "`n") { if ($Text[$j] -eq '\') { $j++ }; $j++ }
            & $blank $i ($j + 1); $i = $j + 1; continue
        }
        $third = if ($i + 2 -lt $n) { $Text[$i + 2] } else { [char]0 }
        if ($ch -eq '"' -or (($ch -eq '@' -or $ch -eq '$') -and ($nx -eq '"' -or (($nx -eq '@' -or $nx -eq '$') -and $third -eq '"')))) {
            $s = $i; $verbatim = $false; $interp = $false
            while ($Text[$i] -ne '"') { if ($Text[$i] -eq '@') { $verbatim = $true } else { $interp = $true }; $i++ }
            if ($i + 2 -lt $n -and $Text[$i + 1] -eq '"' -and $Text[$i + 2] -eq '"') {
                $e = $Text.IndexOf('"""', $i + 3); $e = if ($e -lt 0) { $n } else { $e + 3 }; & $blank $s $e; $i = $e; continue
            }
            $i++; $hole = 0
            while ($i -lt $n) {
                $x = $Text[$i]; $y = if ($i + 1 -lt $n) { $Text[$i + 1] } else { [char]0 }
                if ($hole -gt 0) {
                    if ($x -eq '"') { $i++; while ($i -lt $n -and $Text[$i] -ne '"') { if ($Text[$i] -eq '\') { $i++ }; $i++ } }
                    elseif ($x -eq '{') { $hole++ } elseif ($x -eq '}') { $hole-- }
                    $i++; continue
                }
                if ($interp -and $x -eq '{') { if ($y -eq '{') { $i += 2 } else { $hole = 1; $i++ }; continue }
                if ($verbatim -and $x -eq '"') { if ($y -eq '"') { $i += 2; continue }; break }
                if (!$verbatim -and $x -eq '\') { $i += 2; continue }
                if ($x -eq '"' -or (!$verbatim -and $x -eq "`n")) { break }
                $i++
            }
            & $blank $s ($i + 1); $i++; continue
        }
        $i++
    }
    -join $c
}

# The bodies of the methods marked [MethodReload]: @{ Name; Start; Length } each, the body being `{ ... }` or
# `=> ...;`. $null when the scan cannot follow the file (unbalanced braces).
function Get-LiveReloadBodies([string]$Text) {
    $m = ConvertTo-LiveCodeMask $Text
    $n = $m.Length
    if (($m.Split('{').Count) -ne ($m.Split('}').Count)) { return $null }
    $attr = [regex]'(?<=[\[,]\s*)(?:global::)?(?:Unity\.Pipeline\.MethodReload\.)?MethodReload(?:Attribute)?\s*(?=[\],(])'
    $close = @{ '(' = ')'; '{' = '}'; '[' = ']' }
    # the index just past the bracket that closes the one at $At, or -1
    $match = { param($At)
        $stack = [Collections.Generic.Stack[string]]::new()
        for ($k = $At; $k -lt $n; $k++) {
            $ch = [string]$m[$k]
            if ($close.ContainsKey($ch)) { $stack.Push($close[$ch]) }
            elseif (')}]'.Contains($ch)) { if (!$stack.Count -or $stack.Pop() -cne $ch) { return -1 }; if (!$stack.Count) { return $k + 1 } }
        }
        -1 }
    $out = [Collections.Generic.List[object]]::new()
    foreach ($a in $attr.Matches($m)) {
        $i = $a.Index + $a.Length; $depth = 1
        while ($i -lt $n -and -not ($depth -eq 0 -and $m[$i] -eq '(')) {
            if ($m[$i] -eq '[') { $depth++ } elseif ($m[$i] -eq ']') { $depth-- }
            $i++
        }
        if ($i -ge $n) { return $null }
        if ($m.Substring(0, $i) -notmatch '(\w+)\s*(<[^<>]*>)?\s*$') { return $null }
        $name = $Matches[1]
        $i = & $match $i
        if ($i -lt 0) { return $null }
        while ($i -lt $n -and $m[$i] -ne '{' -and $m[$i] -ne ';' -and -not ($m[$i] -eq '=' -and $i + 1 -lt $n -and $m[$i + 1] -eq '>')) { $i++ }
        if ($i -ge $n -or $m[$i] -eq ';') { continue }   # no body (abstract, extern, partial)
        if ($m[$i] -eq '{') { $end = & $match $i }
        else {
            $end = -1; $k = $i + 2
            while ($k -lt $n) {
                if ($m[$k] -eq ';') { $end = $k + 1; break }
                if ('({['.Contains([string]$m[$k])) { $k = & $match $k; if ($k -lt 0) { break } } else { $k++ }
            }
        }
        if ($end -lt 0 -or ($out.Count -and $i -lt $out[-1].Start + $out[-1].Length)) { return $null }
        $out.Add(@{ Name = $name; Start = $i; Length = $end - $i })
    }
    , $out.ToArray()
}

# Whether a .cs edit can be hot-reloaded in Play: every change sits inside the body of a [MethodReload] method, and the
# rest of the file (signatures, fields, members, usings, attributes) is the same up to whitespace. @{ Reload; Reason;
# Methods } with Reason naming the first change that forces a recompile.
function Test-LiveMethodReload([string]$Old, [string]$New) {
    $a = Get-LiveReloadBodies $Old; $b = Get-LiveReloadBodies $New
    if ($null -eq $a -or $null -eq $b) { return @{ Reload = $false; Reason = 'the braces could not be followed'; Methods = @() } }
    if (!$b.Count) { return @{ Reload = $false; Reason = 'no [MethodReload] method in the file'; Methods = @() } }
    $skeleton = { param($Text, $Bodies)
        $s = $Text
        foreach ($x in @($Bodies) | Sort-Object { $_.Start } -Descending) { $s = $s.Remove($x.Start, $x.Length).Insert($x.Start, '{}') }
        , @($s -split "`r?`n" | ForEach-Object { ($_ -replace '\s+', ' ').Trim() } | Where-Object { $_ }) }
    $sa = & $skeleton $Old $a; $sb = & $skeleton $New $b
    for ($k = 0; $k -lt [Math]::Max($sa.Count, $sb.Count); $k++) {
        if ($sa[$k] -cne $sb[$k]) {
            $what = if ($k -ge $sb.Count -or ($sa.Count -gt $sb.Count -and $sa[$k + 1] -ceq $sb[$k])) { "removed '$($sa[$k])'" }
                    elseif ($k -ge $sa.Count -or ($sb.Count -gt $sa.Count -and $sb[$k + 1] -ceq $sa[$k])) { "added '$($sb[$k])'" }
                    else { "'$($sb[$k])'" }
            return @{ Reload = $false; Reason = "changed outside a [MethodReload] body: $what"; Methods = @() }
        }
    }
    $changed = @(for ($k = 0; $k -lt $b.Count; $k++) {
        if ($Old.Substring($a[$k].Start, $a[$k].Length) -cne $New.Substring($b[$k].Start, $b[$k].Length)) { $b[$k].Name } })
    @{ Reload = $true; Reason = $null; Methods = $changed }
}

# The route of a whole sync and why: a hot reload only when every change is a reloadable .cs (an import beside it, or
# after it while reloaded code is uncompiled, would compile that code in Play), else Select-LiveRoute.
function Select-LiveSyncRoute([string[]]$Routes, [bool]$ReloadPending) {
    $other = @($Routes | Where-Object { $_ -notin 'reload', 'none' })
    if (@($Routes) -contains 'reload') {
        if (!$other) { return @{ Route = 'reload'; Why = $null } }
        if ($other -notcontains 'recompile') { return @{ Route = 'recompile'; Why = 'a hot reload cannot share a sync with an import' } }
        return @{ Route = 'recompile'; Why = $null }
    }
    $r = Select-LiveRoute $other
    if ($ReloadPending -and $r -in 'replay', 'style', 'asset') { return @{ Route = 'recompile'; Why = 'hot-reloaded code is not compiled yet: an import would compile it in Play' } }
    @{ Route = $r; Why = $null }
}

# An asmdef's JSON with the Pipeline's two assemblies referenced (the attribute, and the weaver's runtime); the input
# unchanged when it has both.
function Add-LiveReloadAsmdef([string]$Json) {
    $o = $Json | ConvertFrom-Json -AsHashtable
    $refs = @($o['references'] | Where-Object { $_ })
    $missing = @('Unity.Pipeline', 'Unity.Pipeline.Attributes' | Where-Object { $refs -notcontains $_ })
    if (!$missing) { return $Json }
    $o['references'] = @($refs + $missing)
    ($o | ConvertTo-Json -Depth 6) + "`n"
}

# ProjectSettings.asset's YAML with UNIDAD_LIVE in the Standalone scripting defines; the input unchanged when it is
# there. A project define, not an asmdef versionDefine: the Pipeline parses a reloaded file under the project's defines
# (EditorUserBuildSettings.activeScriptCompilationDefines), so an asmdef-only symbol hides the attribute from it.
function Add-LiveDefine([string]$Yaml) {
    $nl = if ($Yaml.Contains("`r`n")) { "`r`n" } else { "`n" }
    $head = [regex]::Match($Yaml, '(?m)^  scriptingDefineSymbols:( \{\})?(?=\r?$)')
    if (!$head.Success) { return $Yaml }
    $entry = "$nl    Standalone: UNIDAD_LIVE"
    if ($head.Groups[1].Success) { return $Yaml.Remove($head.Index, $head.Length).Insert($head.Index, "  scriptingDefineSymbols:$entry") }
    $end = $head.Index + $head.Length
    $block = [regex]::Match($Yaml.Substring($end), '^(\r?\n    [^\r\n]*)*')
    $sa = [regex]::Match($block.Value, '(?m)^    Standalone: ?([^\r\n]*)')
    if (!$sa.Success) { return $Yaml.Insert($end, $entry) }
    $old = $sa.Groups[1].Value
    if (@($old -split ';') -contains 'UNIDAD_LIVE') { return $Yaml }
    $at = $end + $sa.Groups[1].Index
    $Yaml.Remove($at, $old.Length).Insert($at, ((@($old -split ';' | Where-Object { $_ }) + 'UNIDAD_LIVE') -join ';'))
}

# The .cs files under $Dir that Unity can see: no Library/Temp/Logs/obj/UserSettings, no dot or `~` folders.
function Get-LiveSourceFiles([string]$Dir) {
    foreach ($d in [IO.Directory]::EnumerateDirectories($Dir)) {
        $leaf = [IO.Path]::GetFileName($d)
        if ($leaf -match '^(\..*|Library|Temp|Logs|obj|UserSettings|Build)$' -or $leaf.EndsWith('~')) { continue }
        Get-LiveSourceFiles $d
    }
    [IO.Directory]::EnumerateFiles($Dir, '*.cs')
}

# In the live slot only: UNIDAD_LIVE in the project's defines, and every asmdef whose assembly has a .cs naming
# MethodReload gets Add-LiveReloadAsmdef. Runs after every copy and prepare (either may restore a file). Returns the
# files it rewrote. shortcut: reads every visible .cs of the slot each call; cache the list if a large game's -Sync feels it.
function Install-LiveMethodReload([string]$SlotRoot, [string]$ProjectPath) {
    $settings = Join-Path $ProjectPath 'ProjectSettings/ProjectSettings.asset'
    if (Test-Path -LiteralPath $settings) {
        $text = [IO.File]::ReadAllText($settings); $new = Add-LiveDefine $text
        if ($new -cne $text) { [IO.File]::WriteAllText($settings, $new); $settings }
    }
    $asmdefs = @{}
    foreach ($f in Get-LiveSourceFiles $SlotRoot) {
        if (![IO.File]::ReadAllText($f).Contains('MethodReload')) { continue }
        for ($d = [IO.Path]::GetDirectoryName($f); $d -and $d.Length -ge $SlotRoot.Length; $d = [IO.Path]::GetDirectoryName($d)) {
            $a = @([IO.Directory]::GetFiles($d, '*.asmdef'))
            if ($a) { $asmdefs[$a[0]] = $true; break }
        }
    }
    @(foreach ($a in $asmdefs.Keys) {
        $text = [IO.File]::ReadAllText($a)
        if (($text | ConvertFrom-Json).noEngineReferences) { Write-Host "live: $a is engine-free: [MethodReload] needs an engine assembly"; continue }
        $new = Add-LiveReloadAsmdef $text
        if ($new -cne $text) { [IO.File]::WriteAllText($a, $new); $a }
    })
}

# The file Unity compiles for a changed path: the path itself, or for a file in a `~` folder (a sample a prepare step
# copies) the visible copy with the same name and content under the project. $null when there is none.
function Resolve-LiveCompiledFile([string]$SlotRoot, [string]$ProjectPath, [string]$Path) {
    $full = (Join-Path $SlotRoot $Path) -replace '\\', '/'
    if ($Path -notmatch '~(/|\\)') { return $full }
    $hash = (Get-FileHash -LiteralPath $full).Hash
    Get-LiveSourceFiles $ProjectPath | Where-Object { [IO.Path]::GetFileName($_) -eq [IO.Path]::GetFileName($full) -and (Get-FileHash -LiteralPath $_).Hash -eq $hash } |
        ForEach-Object { $_ -replace '\\', '/' } | Select-Object -First 1
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
