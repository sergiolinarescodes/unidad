<#
The reference pack (U-43, U-59): the package DLLs a green lane compile left in a Unity slot's Library/ScriptAssemblies,
copied per repo and SHA into <home>/refpacks/<repo>/<sha>/ (editor DLLs in Editor/), then current.txt swapped
atomically. Engine module DLLs are not copied: Check projects reference the install's Data/Managed directly (U-130).
  refpack.ps1 [-Slot <name>] [-Sha <sha>] [-Prune]
  refpack.ps1 -SeedAnalyzer <unidad checkout>    the committed analyzer DLL of that repo's origin/main -> <refpacks>/<repo>/analyzer/
Exit 0; 1 the slot's last compile was not green for that SHA; 2 environment.
#>
param([string]$Slot, [string]$Sha, [switch]$Prune, [string]$SeedAnalyzer)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/Lane.ps1')
$main = Get-MainCheckout $PSScriptRoot
$cfg = Get-RepoConfig (Get-RepoRoot $PSScriptRoot)
$repoDir = Join-Path (Get-ToolsHome) "refpacks/$($cfg.name)"
New-Item -ItemType Directory -Force $repoDir | Out-Null

function Invoke-Prune {
    $current = Get-RefpackPointer $cfg.name
    $dirs = @(Get-ChildItem -LiteralPath $repoDir -Directory | Where-Object { $_.Name -match '^[0-9a-f]{40}$' } | Sort-Object LastWriteTimeUtc -Descending)
    foreach ($d in $dirs | Select-Object -Skip 2) {
        if ($current -and ([IO.Path]::GetFullPath($d.FullName) -eq [IO.Path]::GetFullPath($current))) { continue }
        if ($d.LastWriteTimeUtc -gt [DateTime]::UtcNow.AddHours(-1)) { continue }
        Remove-Item -LiteralPath $d.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "refpack: pruned $($d.Name)"
    }
}

if ($SeedAnalyzer) {
    $src = Get-RepoRoot $SeedAnalyzer
    git -C $src fetch -q origin 2>$null
    $out = Join-Path $repoDir 'analyzer'
    New-Item -ItemType Directory -Force $out | Out-Null
    $psi = [Diagnostics.ProcessStartInfo]::new('git')
    foreach ($a in '-C', $src, 'show', 'origin/main:Packages/com.unidad.core/Analyzers/Unidad.Analyzers.dll') { $psi.ArgumentList.Add($a) }
    $psi.RedirectStandardOutput = $true; $psi.UseShellExecute = $false
    $p = [Diagnostics.Process]::Start($psi)
    $tmp = Join-Path $out 'Unidad.Analyzers.dll.tmp'
    $fs = [IO.File]::Create($tmp); $p.StandardOutput.BaseStream.CopyTo($fs); $fs.Dispose(); $p.WaitForExit()
    if ($p.ExitCode -ne 0 -or (Get-Item $tmp).Length -eq 0) { Remove-Item $tmp -Force; Write-Host "refpack: $src origin/main has no committed analyzer DLL"; exit 2 }
    Move-Item -Force $tmp (Join-Path $out 'Unidad.Analyzers.dll')
    Write-Host "refpack: analyzer -> $out/Unidad.Analyzers.dll"
    exit 0
}
if ($Prune -and !$Slot) { Invoke-Prune; exit 0 }

$slotName = if ($Slot) { $Slot } else { @($cfg.unitySlots)[0] }
$slotPath = Get-SlotPath $cfg (Split-Path $main -Parent) $slotName
$proj = Get-UnityProjectPath $cfg $slotPath
if (!(Test-Path -LiteralPath $proj)) { Write-Host "refpack: no Unity slot at $slotPath"; exit 2 }
if (!$Sha) { $Sha = (git -C $slotPath rev-parse HEAD).Trim() }
$state = Read-LaneState $proj
if (!$state.LastCompile -or $state.LastCompile.Sha -ne $Sha -or $state.LastCompile.Result -ne 'green') {
    Write-Host "refpack: $slotName's last compile is not green for $Sha ($(if ($state.LastCompile) { "$($state.LastCompile.Result) at $($state.LastCompile.Sha)" } else { 'none' }))"
    exit 1
}
$names = @(Get-ChildItem -LiteralPath "$proj/Library/ScriptAssemblies" -Filter '*.dll' -ErrorAction SilentlyContinue | ForEach-Object Name)
$pick = Select-RefpackFiles $names $cfg
if (!$pick.Runtime) { Write-Host "refpack: $proj/Library/ScriptAssemblies holds no package DLL"; exit 2 }
$dest = Join-Path $repoDir $Sha
$tmp = "$dest.tmp-$PID"
New-Item -ItemType Directory -Force "$tmp/Editor" | Out-Null
foreach ($n in $pick.Runtime) { Copy-Item -LiteralPath "$proj/Library/ScriptAssemblies/$n" $tmp }
foreach ($n in $pick.Editor) { Copy-Item -LiteralPath "$proj/Library/ScriptAssemblies/$n" "$tmp/Editor" }
if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force }
Move-Item -LiteralPath $tmp $dest
Write-RefpackPointer $repoDir $dest
Write-Host "refpack: $($cfg.name)/current.txt -> $($dest -replace '\\', '/') ($($pick.Runtime.Count) runtime, $($pick.Editor.Count) editor DLLs)"
Invoke-Prune
exit 0
