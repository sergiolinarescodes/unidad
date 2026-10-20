<#
.SYNOPSIS
  The release gate of unidad (U-118): the sidecar build, the smoke projects of the touched packages, then the lane's
  MSBuild-mode compile and Bee compile of the Sandbox, both under ONE hold of the machine-wide editor lock (U-55), and a
  scan of their output for compiler errors (the console may be localised; compiler ids are not).
  The lane reads the local branch: commit before running this.
  Exit 0 green | 1 build/test/compile red (the failing step and the error lines) | 2 setup | 3 editor-lock timeout
.EXAMPLE
  pwsh -File tools/sandbox-gate.ps1
  pwsh -File tools/sandbox-gate.ps1 -Touched core,unity -Branch sync/pre.2
  pwsh -File tools/sandbox-gate.ps1 -WhatIf
#>
param(
    [string[]]$Touched = @(),
    [string]$Branch,
    [string]$UnityExe,
    [switch]$WhatIf,
    [int]$WaitMinutes = 60,
    # Test seams: another lane script, and no dotnet steps.
    [string]$Lane = (Join-Path $PSScriptRoot 'unity-lane.ps1'),
    [switch]$SkipDotnet
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/EditorLock.ps1')

$Root = Get-RepoRoot $PSScriptRoot
$cfg = Get-RepoConfig $Root
$SmokeProjects = [ordered]@{
    core      = @('dotnet/Unidad.Core.Smoke')
    modules   = @('Launch', 'Flow', 'Text', 'Save', 'Audio' | ForEach-Object { "dotnet/Unidad.$_.Smoke" })
    unity     = @('dotnet/Unidad.Adapter.Smoke')
    analyzers = @('analyzers/Unidad.Analyzers.Smoke')
}
$ErrorLine = '\berror (CS|UD|UAC)\d{3,4}\b'

$Touched = @($Touched | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
$bad = @($Touched | Where-Object { $_ -notin $SmokeProjects.Keys })
if ($bad) { Write-Host "sandbox-gate: unknown -Touched $($bad -join ',') (expected $($SmokeProjects.Keys -join ','))"; exit 2 }
$keys = if ($Touched) { $Touched } else { @($SmokeProjects.Keys) }
$smoke = @($keys | ForEach-Object { $SmokeProjects[$_] } | Where-Object { Test-Path -LiteralPath (Join-Path $Root $_) })
if (!$Branch) { $Branch = git -C $Root branch --show-current }
if (!$Branch) { Write-Host 'sandbox-gate: detached HEAD; pass -Branch <b> (the lane compiles a local branch)'; exit 2 }
$laneArgs = @('-Branch', $Branch) + $(if ($UnityExe) { @('-UnityPath', $UnityExe) })
$sln = $cfg.sidecar.solution

if ($WhatIf) {
    if (!$SkipDotnet) {
        Write-Host "1. {dotnet} build $sln -v:q -nologo"
        $smoke | ForEach-Object { Write-Host "2. {dotnet} test $_ -v:q --nologo" }
    }
    Write-Host "3. under the editor lock: pwsh -File $Lane -Compile $($laneArgs -join ' ')"
    Write-Host "4. under the editor lock: pwsh -File $Lane -Bee $($laneArgs -join ' ')"
    Write-Host "5. scan both outputs for '$ErrorLine'"
    exit 0
}

if (!$SkipDotnet) {
    try { $dotnet = Resolve-UnityDotnet (Resolve-UnityEditor (Get-UnityProjectPath $cfg $Root) $UnityExe $cfg.editorVersion) }
    catch { Write-Host "sandbox-gate: $($_.Exception.Message)"; exit 2 }
    if (!(Test-Path -LiteralPath $dotnet)) { Write-Host "sandbox-gate: no bundled SDK at $dotnet"; exit 2 }
    $steps = @(, @('build', $sln, '-v:q', '-nologo')) + @($smoke | ForEach-Object { , @('test', $_, '-v:q', '--nologo') })
    foreach ($s in $steps) {
        Write-Host "sandbox-gate: dotnet $($s -join ' ')"
        Push-Location $Root
        try { $out = @(& $dotnet @s 2>&1 | ForEach-Object { "$_" }) } finally { Pop-Location }
        if ($LASTEXITCODE -ne 0) {
            $out | Where-Object { $_ -match $ErrorLine -or $_ -match '\b(error|Failed)\b' } | Select-Object -First 40 | ForEach-Object { Write-Host $_ }
            Write-Host "sandbox-gate: RED at dotnet $($s[0]) $($s[1]) (exit $LASTEXITCODE)"; exit 1
        }
    }
}
if (!(Test-Path -LiteralPath (Get-UnityProjectPath $cfg $Root))) { Write-Host 'sandbox-gate: no Sandbox project'; exit 2 }

# The lane takes the editor lock per Unity launch; it inherits this owner through UNIDAD_EDITOR_LOCK_OWNER, so its
# launches re-enter this one hold and no other agent's editor runs between the compile and the Bee pass.
try {
    $code = Invoke-WithEditorLock -Owner "sandbox-gate:$PID" -TimeoutMinutes 30 -WaitMinutes $WaitMinutes -ScriptBlock {
        foreach ($mode in '-Compile', '-Bee') {
            Write-Host "sandbox-gate: unity-lane $mode $($laneArgs -join ' ')"
            $out = @(& pwsh -NoProfile -File $Lane $mode @laneArgs 2>&1 | ForEach-Object { "$_" })
            $laneCode = $LASTEXITCODE
            $out | ForEach-Object { Write-Host "  $_" }
            if ($laneCode -eq 3) { Write-Host "sandbox-gate: lane $mode timed out on a lock or its cap"; return 3 }
            $errors = @($out | Where-Object { $_ -match $ErrorLine })
            if ($laneCode -ne 0 -or $errors) {
                $errors | ForEach-Object { Write-Host "sandbox-gate: $_" }
                Write-Host "sandbox-gate: RED at lane $mode (exit $laneCode, $($errors.Count) error lines)"
                return $(if ($laneCode -eq 2) { 2 } else { 1 })
            }
        }
        0
    }
} catch [TimeoutException] { Write-Host "sandbox-gate: $($_.Exception.Message)"; exit 3 }
if ($code -eq 0) { Write-Host 'sandbox-gate: green' }
exit $code
