# The serial Unity lane's functions (U-61): pure cores the smoke tests call, plus the slot locks, claims and the one
# tracked Unity launch every mode, refpack.ps1 and testbed.ps1 share. Dot-sourced; names no repo, slot or method.

. (Join-Path $PSScriptRoot 'EditorLock.ps1')
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'launch-guard.ps1')

$script:ClaimIdleMinutes = 20
# MSBuild mode imports neither csc.rsp nor UnityProject.Build.props for packages (Unity.Sdk 1.0.92 Sdk.props skips both
# when CurrentPackageName is set) and defaults them to C# 9, so every MSBuild-mode process gets LangVersion from the
# environment (an MSBuild property; U-06's C# 14 for embedded and file: packages alike).
$script:MsbuildEnv = @{ LangVersion = '14' }
$script:NeverCollect = '(^|/)(Library|Temp|Logs|UserSettings|Build|obj)(/|$)|\.gen\.csproj$|\.slnx$|(^|/)global\.json$|(^|/)NuGet\.config$|(^|/)Main\.EntryPoint\.csproj$'

# --- pure cores ---------------------------------------------------------------------------------------------------

# A claim is released after ClaimIdleMinutes without a lane call by its branch.
function Test-ClaimIdle($Claim, [datetime]$NowUtc = [DateTime]::UtcNow, [int]$IdleMinutes = $script:ClaimIdleMinutes) {
    if (!$Claim -or !$Claim.LastUsed) { return $true }
    ($NowUtc - (ConvertTo-LockUtc $Claim.LastUsed)).TotalMinutes -ge $IdleMinutes
}

function Resolve-ExecMethod([string]$Method, [string]$Prefix) {
    if ($Method -match '\.') { return $Method }
    "$Prefix$Method"
}

# Unity.exe arguments per mode. MSBuild mode (-enable-msbuild, it does not persist) everywhere except Bee and the player.
function Get-UnityArgs([string]$Mode, [string]$ProjectPath, [string]$LogFile, [string]$Method, [string]$BuildOut,
                       [switch]$Release, [string[]]$Extra = @()) {
    $a = @('-batchmode', '-nographics')
    if ($Mode -notin 'bee', 'player') { $a += '-enable-msbuild' }
    $a += @('-quit', '-projectPath', $ProjectPath)
    if ($LogFile) { $a += @('-logFile', $LogFile) }
    switch ($Mode) {
        'exec' { $a += @('-executeMethod', $Method) + @($Extra) }
        'player' { $a += @('-buildTarget', 'win64', '-executeMethod', $Method, '-buildOut', $BuildOut, $(if ($Release) { '-release' } else { '-development' })) }
    }
    $a
}

# -Set "k=v;k2=v2,v3": split only before a key= (U-60), so a comma inside a value survives.
function Split-SetString([string]$Set) {
    if (!$Set) { return @() }
    @(foreach ($item in ($Set -split '[;,](?=[a-z][a-z0-9.]*=)')) {
        $t = $item.Trim()
        if (!$t) { continue }
        if ($t -notmatch '^[a-z][a-z0-9.]*=\S*$') { throw "-Set '$t' must be key=value (such as seed=7)" }
        $t
    })
}

# The launch line, every setting as --set (U-44). -Base tokens (repo.json playerBootArgs) come first; a later key wins.
function ConvertTo-LaunchLine([string]$Start, [string]$Set, [int]$Frames, [string[]]$Base = @(), [hashtable]$Extra = @{}) {
    $settings = [ordered]@{}
    for ($i = 0; $i -lt $Base.Count; $i++) {
        if ($Base[$i] -eq '--set' -and $i + 1 -lt $Base.Count) { $k, $v = $Base[++$i] -split '=', 2; $settings[$k] = $v }
    }
    if ($Start) { $settings['start'] = $Start }
    foreach ($item in Split-SetString $Set) { $k, $v = $item -split '=', 2; $settings[$k] = $v }
    if ($Frames -gt 0) { $settings['frames'] = "$Frames" }
    foreach ($k in $Extra.Keys) { $settings[$k] = $Extra[$k] }
    @(foreach ($k in $settings.Keys) { '--set'; "$k=$($settings[$k])" })
}

function Get-PlayerRunArgs([string[]]$LaunchLine) { @('-batchmode', '-nographics', '--') + @($LaunchLine) }

# Unique "error CS####" / "error UD###" lines, in first-seen order.
function Get-CompileErrors([string[]]$Lines) {
    $seen = [Collections.Generic.List[string]]::new()
    foreach ($l in $Lines) {
        $t = "$l".Trim()
        if ($t -match 'error (CS|UD|UAC)\d{3,4}' -and !$seen.Contains($t)) { $seen.Add($t) }
    }
    $seen.ToArray()
}

function Get-ListHash([string[]]$Paths) {
    $text = (@($Paths | Where-Object { $_ } | Sort-Object { $_ } -CaseSensitive) -join "`n")
    -join ([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)) | ForEach-Object { $_.ToString('x2') })
}

# The asmdef/.cs list of a checkout (U-61): a new .cs regenerates unless the generated projects glob (C7).
function Get-CheckoutListHash([string]$Root, [bool]$MsbuildGlobs) {
    $patterns = @('*.asmdef', '*.asmref', '*manifest.json')
    if (!$MsbuildGlobs) { $patterns += '*.cs' }
    Get-ListHash @(git -C $Root -c core.quotepath=off ls-files -- @patterns)
}

# A .slnx is usable only while every project it lists exists (a purged .gen.csproj leaves a dangling entry).
function Test-SlnxComplete([string]$Slnx) {
    $dir = Split-Path -Parent $Slnx
    @(([xml](Get-Content -Raw -LiteralPath $Slnx)).SelectNodes('//Project') | Where-Object { !(Test-Path -LiteralPath (Join-Path $dir $_.Path)) }).Count -eq 0
}

function Test-RegenerateNeeded($State, [string]$NewHash, [bool]$SlnxExists, [bool]$MsbuildDirExists = $true) {
    !$SlnxExists -or !$MsbuildDirExists -or !$State -or $State.GeneratedListHash -ne $NewHash
}

# -Compile's decision core: regenerate when needed, then build; exit 0 green / 1 red / the regenerate's code >= 3.
function Invoke-CompileCore($State, [string]$NewHash, [scriptblock]$SlnxExists, [scriptblock]$Regenerate, [scriptblock]$Build) {
    $regenerated = $false
    if (Test-RegenerateNeeded $State $NewHash (& $SlnxExists)) {
        $code = & $Regenerate
        $regenerated = $true
        if ($code -ge 3) { return @{ Code = $code; Errors = @(); Regenerated = $true } }
        if (!(& $SlnxExists)) { return @{ Code = 1; Errors = @('regenerate produced no .slnx'); Regenerated = $true } }
    }
    $r = & $Build
    $errors = @(Get-CompileErrors @($r.Lines))
    @{ Code = $(if ($r.Code -eq 0) { 0 } else { 1 }); Errors = $errors; Regenerated = $regenerated }
}

# A -Run reuses <slot>/Build when its stamp equals the branch SHA.
function Test-BuildReusable([string]$Stamp, [string]$Sha, [bool]$ExeExists) { $ExeExists -and $Stamp -and $Stamp.Trim() -eq $Sha }

# U-59: package and engine-module DLLs only, plus Unidad.Adapter(.Editor); never Unidad.Core/Modules, never game code.
function Select-RefpackFiles([string[]]$Names, $Cfg) {
    $runtime = @(); $editor = @()
    foreach ($n in $Names) {
        if ($n -match '^Unidad\.' -and $n -notmatch '^Unidad\.Adapter(\.Editor)?\.dll$') { continue }
        if ($n -match '^Assembly-CSharp') { continue }
        if (Test-MatchesAny @($n) @($Cfg.refpack.editorInclude)) { $editor += $n }
        elseif (Test-MatchesAny @($n) @($Cfg.refpack.include)) { $runtime += $n }
    }
    @{ Runtime = $runtime; Editor = $editor }
}

# current.txt holds the absolute directory, forward slashes, no newline; written beside and moved over (U-43).
function Write-RefpackPointer([string]$RepoDir, [string]$PackDir) {
    New-Item -ItemType Directory -Force $RepoDir | Out-Null
    $tmp = Join-Path $RepoDir 'current.txt.tmp'
    [IO.File]::WriteAllText($tmp, ($PackDir -replace '\\', '/'))
    Move-Item -Force -LiteralPath $tmp -Destination (Join-Path $RepoDir 'current.txt')
}

# Porcelain v1 lines -> the paths under the collect roots (repo-relative), never Library/Temp/... or generated files.
function Select-CollectPaths([string[]]$Porcelain, [string[]]$Roots) {
    $roots = @($Roots | ForEach-Object { ($_ -replace '\\', '/').Trim('/') } | Where-Object { $_ })
    @(foreach ($line in $Porcelain) {
        if ("$line".Length -lt 4) { continue }
        $xy = $line.Substring(0, 2); $path = $line.Substring(3).Trim('"')
        if ($path -match ' -> ') { $path = ($path -split ' -> ')[-1].Trim('"') }
        if ($path -match $script:NeverCollect) { continue }
        if (!($roots | Where-Object { $path -eq $_ -or $path.StartsWith("$_/") })) { continue }
        [pscustomobject]@{ Path = $path; Deleted = $xy -match 'D' }
    })
}

# Collect roots are relative to the Unity project ("../tools/meta-templates" reaches the repo); returns repo-relative.
function Resolve-CollectRoots([string[]]$Roots, [string]$ProjectSubdir) {
    @(foreach ($r in $Roots) {
        $joined = if ($ProjectSubdir -and $ProjectSubdir -ne '.') { "$ProjectSubdir/$r" } else { $r }
        $parts = [Collections.Generic.List[string]]::new()
        foreach ($s in ($joined -replace '\\', '/') -split '/') {
            if ($s -eq '..') { if ($parts.Count) { $parts.RemoveAt($parts.Count - 1) } } elseif ($s -and $s -ne '.') { $parts.Add($s) }
        }
        $parts -join '/'
    })
}

# U-61: the local refs/heads/<b> (slots share one object store, so no push is needed), else origin/<b>.
function Resolve-LaneCommit([string]$Root, [string]$Branch) {
    foreach ($kind in @(@('local', "refs/heads/$Branch"), @('origin', "refs/remotes/origin/$Branch"))) {
        $sha = git -C $Root rev-parse --verify -q "$($kind[1])^{commit}" 2>$null
        if ($LASTEXITCODE -eq 0 -and $sha) { return @{ Sha = $sha.Trim(); Source = $kind[0] } }
    }
    $null
}

# U-60: an open branch previews as a merge with the default branch; a merged or missing one shows the default branch.
function Resolve-TestbedTarget([string]$Root, [string]$Branch, [string]$MainBranch, [string]$Ref) {
    $main = (git -C $Root rev-parse --verify -q "refs/remotes/origin/$MainBranch^{commit}" 2>$null)
    if ($Ref) {
        $sha = git -C $Root rev-parse --verify -q "$Ref^{commit}" 2>$null
        if ($LASTEXITCODE -ne 0 -or !$sha) { throw "-Ref $Ref is not a commit here" }
        return @{ Kind = 'ref'; Sha = $sha.Trim(); Note = "showing $Ref ($($sha.Trim().Substring(0,7)))" }
    }
    if (!$main) { throw "origin/$MainBranch is missing; fetch first" }
    $main = $main.Trim()
    $head = git -C $Root rev-parse --verify -q "refs/remotes/origin/$Branch^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0 -or !$head) { return @{ Kind = 'default'; Sha = $main; Note = "$Branch is gone from origin; showing $MainBranch" } }
    $head = $head.Trim()
    git -C $Root merge-base --is-ancestor $head $main 2>$null
    if ($LASTEXITCODE -eq 0) { return @{ Kind = 'default'; Sha = $main; Note = "$Branch merged at $($head.Substring(0,7)); showing $MainBranch" } }
    @{ Kind = 'merge'; Sha = $main; Head = $head; Note = "$Branch ($($head.Substring(0,7))) merged into $MainBranch ($($main.Substring(0,7))), uncommitted" }
}

# --- state: slot locks, claims, lane state --------------------------------------------------------------------------

function Get-SlotLockDir {
    $dir = Join-Path (Get-EditorLockDir) 'slots'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $dir
}

function Get-SlotLockPath([string]$Repo, [string]$Slot) { Join-Path (Get-SlotLockDir) "$Repo-$Slot.lock" }
function Get-SlotClaimPath([string]$Repo, [string]$Slot) { Join-Path (Get-SlotLockDir) "$Repo-$Slot.claim" }

# Takes the slot lock for this process (or $ForPid, the testbed editor). True when taken.
function Enter-SlotLock([string]$Repo, [string]$Slot, [string]$Branch, [string]$Mode, [int]$ForPid = $PID) {
    $path = Get-SlotLockPath $Repo $Slot
    if ((Test-Path -LiteralPath $path) -and (Test-LockStale (Read-LockRecord $path))) { Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue }
    $record = [ordered]@{
        Pid = $ForPid; ProcessStart = (Get-ProcessStartUtc $ForPid).ToString('o'); Repo = $Repo; Slot = $Slot; Branch = $Branch
        Mode = $Mode; Owner = $(if ($ForPid -eq $PID) { 'agent' } else { 'user' }); Cwd = (Get-Location).Path -replace '\\', '/'
        Started = [DateTime]::UtcNow.ToString('o')
    }
    try {
        $fs = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $bytes = [Text.Encoding]::UTF8.GetBytes(($record | ConvertTo-Json)); $fs.Write($bytes, 0, $bytes.Length); $fs.Dispose()
        $true
    } catch [IO.IOException] { $false }
}

function Exit-SlotLock([string]$Repo, [string]$Slot) {
    $path = Get-SlotLockPath $Repo $Slot
    $r = Read-LockRecord $path
    if ($r -and $r.Pid -eq $PID) { Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue }
}

# Hands the slot lock to another PID (the testbed's GUI editor), so it frees itself when that process ends.
function Move-SlotLock([string]$Repo, [string]$Slot, [int]$ToPid, [string]$Branch, [string]$Mode) {
    Remove-Item -LiteralPath (Get-SlotLockPath $Repo $Slot) -Force -ErrorAction SilentlyContinue
    Enter-SlotLock $Repo $Slot $Branch $Mode $ToPid | Out-Null
}

function Read-SlotClaim([string]$Repo, [string]$Slot) {
    $path = Get-SlotClaimPath $Repo $Slot
    $c = Read-LockRecord $path
    if ($c -and (Test-ClaimIdle $c)) { Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue; Write-Host "lane: claim of $Slot by $($c.ClaimedBy) released (idle $script:ClaimIdleMinutes min)"; return $null }
    $c
}

function Write-SlotClaim([string]$Repo, [string]$Slot, [string]$Branch) {
    [ordered]@{ ClaimedBy = $Branch; Claimed = [DateTime]::UtcNow.ToString('o'); LastUsed = [DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Get-SlotClaimPath $Repo $Slot) -Encoding utf8
}

function Read-LaneState([string]$ProjectPath) {
    $f = Join-Path $ProjectPath 'Library/.lane-state.json'
    if (Test-Path -LiteralPath $f) { try { return Get-Content -Raw -LiteralPath $f | ConvertFrom-Json -AsHashtable } catch { } }
    @{}
}

function Write-LaneState([string]$ProjectPath, [hashtable]$State) {
    $lib = Join-Path $ProjectPath 'Library'
    if (!(Test-Path -LiteralPath $lib)) { return }
    $State | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $lib '.lane-state.json') -Encoding utf8
}

# True when an interactive editor holds <project>/Temp/UnityLockfile.
function Test-ProjectOpen([string]$ProjectPath) {
    $f = Join-Path $ProjectPath 'Temp/UnityLockfile'
    if (!(Test-Path -LiteralPath $f)) { return $false }
    try { $s = [IO.File]::Open($f, 'Open', 'ReadWrite', 'None'); $s.Dispose(); $false } catch { $true }
}

# --- the one tracked Unity launch ---------------------------------------------------------------------------------

# Under the machine-wide editor lock: start Unity, tie it to this process, register it, wait with the cap and STOP.
# Returns the exit code; 3 on the lock wait or the cap, 4 on STOP (unity-lane's exit codes).
function Invoke-LaneUnity([string]$Editor, [string[]]$Arguments, [string]$Owner, [int]$CapMinutes, [string]$Root, [string]$Branch,
                          [string]$Mode, [double]$WaitMinutes = 60, [hashtable]$Environment = @{}) {
    try {
        Invoke-WithEditorLock -Owner $Owner -TimeoutMinutes $CapMinutes -WaitMinutes $WaitMinutes -ScriptBlock {
            if (Test-LaunchStopped) { return 4 }
            $psi = [Diagnostics.ProcessStartInfo]::new($Editor)
            foreach ($a in $Arguments) { $psi.ArgumentList.Add($a) }
            foreach ($k in $Environment.Keys) { $psi.Environment[$k] = $Environment[$k] }
            $psi.UseShellExecute = $false
            $p = [Diagnostics.Process]::Start($psi)
            $job = New-KillOnCloseJob $p
            $run = Register-TrackedRun $p 'lane' $Root $Branch $Mode ($CapMinutes * 60) 'agent'
            Write-Host "lane: Unity PID $($p.Id) $Mode on $Root (cap $CapMinutes min)"
            try { $code = Wait-TrackedRun $p ($CapMinutes * 60) } finally { Unregister-TrackedRun $run; $null = $job }
            switch ($code) { 124 { 3 } 125 { 4 } default { $code } }
        }
    } catch [TimeoutException] { Write-Host "lane: $($_.Exception.Message)"; 3 }
}

# Runs a non-Unity child (dotnet, a player) with a cap; returns @{ Code; Lines }.
function Invoke-Captured([string]$Exe, [string[]]$Arguments, [int]$CapSeconds = 1800, [string]$Cwd, [hashtable]$Environment = @{}) {
    $psi = [Diagnostics.ProcessStartInfo]::new($Exe)
    foreach ($a in $Arguments) { $psi.ArgumentList.Add($a) }
    foreach ($k in $Environment.Keys) { $psi.Environment[$k] = $Environment[$k] }
    $psi.UseShellExecute = $false; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    if ($Cwd) { $psi.WorkingDirectory = $Cwd }
    $p = [Diagnostics.Process]::Start($psi)
    $job = New-KillOnCloseJob $p
    $out = $p.StandardOutput.ReadToEndAsync(); $err = $p.StandardError.ReadToEndAsync()
    $code = Wait-TrackedRun $p $CapSeconds
    $null = $job
    @{ Code = $code; Lines = @(($out.Result + "`n" + $err.Result) -split "`r?`n" | Where-Object { $_ }) }
}
