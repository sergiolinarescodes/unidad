<#
The testbed door (U-60): the owner's one manual run per PR, in the warm slot ../<prefix>-testbed.
  pwsh -File tools/testbed.ps1 -Branch <b> [-Start <id>] [-Set "k=v;k2=v2"] [-Player [-Frames N]] [-Project <dir>]
  pwsh -File tools/testbed.ps1 -Ref <sha|branch> ...          an exact commit
  pwsh -File tools/testbed.ps1 -StopAll | -Allow | -List      the launch guard
An open branch is previewed as a detached, uncommitted merge of the default branch and origin/<b>; a merged or missing
branch shows the default branch ("<b> merged at <sha>; showing <main>"). An earlier testbed editor this script opened is
closed first. The editor lock is held only for the compile step; then the GUI editor opens as the user's (never capped
or killed by the tools) with -executeMethod <lane.testbedMethod> and UNIDAD_LAUNCH = the launch line. -Player builds a
player instead and opens it windowed, or with -Frames N runs it headless and exits with its code. Everything is passed
as --set (U-44): -Start x is --set start=x; -Set is one string split only before a key=.
An agent never opens a window: in an agent session only -Player -Frames N runs. Exit: 0 launched (or the self-quit run's
own code); 1 the precheck compile or the merge failed; 2 environment; 4 STOP; 5 the slot is open in another editor.
#>
param(
    [string]$Branch, [string]$Ref, [string]$Start, [string]$Set, [switch]$Player, [int]$Frames, [string]$Project,
    [switch]$StopAll, [switch]$Allow, [switch]$List, [string]$UnityPath, [double]$WaitMinutes = 60
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/Lane.ps1')
if ($StopAll -or $Allow -or $List) {
    if ($StopAll) { Stop-AllLaunches } elseif ($Allow) { Enable-Launches }
    Show-Launches
    exit 0
}
if (Test-LaunchStopped) { Write-Host "[testbed] launches are STOPPED ($(Get-StopFile)); the owner lifts it with -Allow"; exit 4 }
if (!$Branch -and !$Ref) { Write-Host 'usage: testbed.ps1 -Branch <b> | -Ref <sha> [-Start id] [-Set "k=v;..."] [-Player [-Frames N]] [-Project dir] | -StopAll | -Allow | -List'; exit 2 }
if ((Test-AgentSession) -and !($Player -and $Frames -gt 0)) {
    Write-Host '[testbed] An agent never opens a window: leave the Testbed line for the owner, or run -Player -Frames N'
    exit 2
}
try { $line = ConvertTo-LaunchLine -Start $Start -Set $Set -Frames $Frames } catch { Write-Host "[testbed] $($_.Exception.Message)"; exit 2 }

$main = Get-MainCheckout $PSScriptRoot
$cfg = Get-RepoConfig (Get-RepoRoot $PSScriptRoot)
if ($Project) { $cfg.projectSubdir = $Project }
if (@($cfg.lane.playerBootArgs) -match '\{report\}' -and $Set -notmatch '(^|[;,])report=') {
    $line = ConvertTo-LaunchLine -Start $Start -Set $Set -Frames $Frames -Extra @{ report = (Join-Path $env:TEMP "$($cfg.name)-report.txt") -replace '\\', '/' }
}
$repo = $cfg.name
$slotName = $cfg.testbedSlot
$slotPath = Get-SlotPath $cfg (Split-Path $main -Parent) $slotName
$proj = Get-UnityProjectPath $cfg $slotPath

# Close the testbed editor this script opened earlier (a registered run); never any other process.
foreach ($r in Get-RunRecords | Where-Object { $_.Kind -eq 'testbed-editor' -and ($_.Root -replace '\\', '/') -eq $slotPath }) {
    Write-Host "[testbed] closing the earlier testbed editor PID $($r.Pid)"
    $p = Get-Process -Id $r.Pid -ErrorAction SilentlyContinue
    if ($p) { $p.CloseMainWindow() | Out-Null; if (!$p.WaitForExit(30000)) { $p.Kill($true); $p.WaitForExit() } }
    Unregister-TrackedRun $r.File
}
$deadline = [DateTime]::UtcNow.AddMinutes($WaitMinutes)
while (!(Enter-SlotLock $repo $slotName $(if ($Branch) { $Branch } else { $Ref }) 'testbed')) {
    if ([DateTime]::UtcNow -ge $deadline) { Write-Host "[testbed] the testbed slot stayed busy for $WaitMinutes min"; exit 3 }
    Start-Sleep -Seconds 2
}
$handedOver = $false
try {
    git -C $main fetch -q origin $cfg.mainBranch
    if ($Branch) { git -C $main fetch -q origin $Branch 2>$null }
    $t = Resolve-TestbedTarget $main $Branch $cfg.mainBranch $Ref
    Write-Host "[testbed] $($t.Note)"
    if (!(Test-Path -LiteralPath (Join-Path $slotPath '.git'))) {
        git -C $main worktree prune
        git -C $main worktree add -q --detach $slotPath $t.Sha
        if ($LASTEXITCODE -ne 0) { Write-Host "[testbed] could not create $slotPath"; exit 2 }
        Write-Host "[testbed] new testbed slot ${slotPath}: the first open is a one-time import"
    }
    if (Test-ProjectOpen $proj) { Write-Host "[testbed] $proj is open in another editor; close it first"; exit 5 }
    git -C $slotPath merge --abort 2>$null
    git -C $slotPath reset -q --hard
    git -C $slotPath checkout -q --force --detach $t.Sha
    if ($t.Kind -eq 'merge') {
        git -C $slotPath -c user.name=testbed -c user.email=testbed@localhost merge -q --no-commit --no-ff $t.Head
        if ($LASTEXITCODE -ne 0) {
            git -C $slotPath diff --name-only --diff-filter=U | ForEach-Object { Write-Host "[testbed] conflict: $_" }
            git -C $slotPath merge --abort 2>$null
            Write-Host "[testbed] $Branch does not merge cleanly with $($cfg.mainBranch); merge $($cfg.mainBranch) into it first"
            exit 1
        }
    }
    if ($cfg.lane.prepare -and (Test-Path -LiteralPath (Join-Path $slotPath $cfg.lane.prepare))) {
        Push-Location $slotPath
        try { & pwsh -NoProfile -File $cfg.lane.prepare | Out-Host } finally { Pop-Location }
    }
    $editor = try { Resolve-UnityEditor $proj $UnityPath $cfg.editorVersion } catch { Write-Host "[testbed] $($_.Exception.Message)"; exit 2 }
    $owner = "testbed:${repo}:$PID"
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    New-Item -ItemType Directory -Force "$proj/Logs" | Out-Null
    $label = if ($Branch) { $Branch } else { $Ref }

    # The compile step: the only part under the editor lock.
    $state = Read-LaneState $proj
    $hash = Get-CheckoutListHash $slotPath ([bool]$cfg.lane.msbuildGlobs)
    $slnx = Get-ChildItem -LiteralPath $proj -Filter '*.slnx' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (Test-RegenerateNeeded $state $hash ([bool]$slnx) (Test-Path -LiteralPath "$proj/Library/MSBuild")) {
        $code = Invoke-LaneUnity $editor (Get-UnityArgs 'regenerate' $proj "$proj/Logs/testbed-regenerate-$stamp.log") $owner $cfg.lane.capsMinutes.init $slotPath $label 'regenerate' $WaitMinutes
        if ($code -ge 3) { exit $code }
        $slnx = Get-ChildItem -LiteralPath $proj -Filter '*.slnx' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($slnx) { $state['GeneratedListHash'] = $hash; $state['GeneratedAt'] = [DateTime]::UtcNow.ToString('o'); Write-LaneState $proj $state }
    }
    if ($slnx) {
        $r = Invoke-Captured (Resolve-UnityDotnet $editor) @('build', $slnx.FullName, '-v:q', '-nologo') 1800 $proj
        if ($r.Code -ne 0) { Get-CompileErrors $r.Lines | ForEach-Object { Write-Host $_ }; Write-Host '[testbed] the precheck compile is red'; exit 1 }
    }
    $sha = (git -C $slotPath rev-parse --short HEAD).Trim()

    if ($Player) {
        $buildDir = "$proj/Build"
        $exe = "$buildDir/$($cfg.lane.playerName).exe"
        $code = Invoke-LaneUnity $editor (Get-UnityArgs 'player' $proj "$proj/Logs/testbed-player-$stamp.log" -Method $cfg.lane.playerBuildMethod -BuildOut $buildDir) $owner $cfg.lane.capsMinutes.player $slotPath $label 'player' $WaitMinutes
        if ($code -ne 0 -or !(Test-Path -LiteralPath $exe)) { Write-Host "[testbed] player build exit $code"; exit $(if ($code -ge 3) { $code } else { 1 }) }
        if ($Frames -gt 0) {
            $cap = [Math]::Min(1800, 600 + [int]($Frames / 10))
            Write-Host "[testbed] $exe $((Get-PlayerRunArgs $line) -join ' ')"
            $r = Invoke-Captured $exe (Get-PlayerRunArgs $line) $cap $buildDir
            Write-Host "[testbed] player exited $($r.Code) after $Frames frames"
            exit $r.Code
        }
        $p = Start-Process -FilePath $exe -ArgumentList (@('-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900', '--') + $line | ForEach-Object { '"' + $_ + '"' }) -WorkingDirectory $buildDir -PassThru
        Register-TrackedRun $p 'player' $slotPath $label 'testbed' 0 'user' | Out-Null
        Write-Host "[testbed] player PID $($p.Id) on $slotName at ${sha}: the window is yours to close"
        exit 0
    }

    $launch = ($line | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    $psi = [Diagnostics.ProcessStartInfo]::new($editor)
    foreach ($a in '-projectPath', $proj, '-executeMethod', $cfg.lane.testbedMethod) { $psi.ArgumentList.Add($a) }
    $psi.Environment['UNIDAD_LAUNCH'] = $launch
    $psi.UseShellExecute = $false
    $p = [Diagnostics.Process]::Start($psi)
    Register-TrackedRun $p 'testbed-editor' $slotPath $label 'editor' 0 'user' | Out-Null
    Move-SlotLock $repo $slotName $p.Id $label 'testbed-editor'
    $handedOver = $true
    Write-Host "[testbed] UNIDAD_LAUNCH=$launch"
    Write-Host "[testbed] Unity PID $($p.Id) on $slotName at ${sha}: the editor is yours to close"
    exit 0
} finally {
    if (!$handedOver) { Exit-SlotLock $repo $slotName }
}
