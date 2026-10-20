# Installs the review-and-merge loop on THIS machine (U-45): a dedicated clone next to the repo (../<name>-loop), the
# one GitHub label it uses, and ONE Windows scheduled task (unidad-loop-<name>) that runs every loop.everyMinutes,
# one run at a time. Idempotent. Installing it is the user's call. `-Uninstall` removes the task (keeps the clone).
#   pwsh -File tools/loop/install.ps1 [-Provider claude|codex] [-LoggedOut] [-Uninstall]
# Default: an Interactive task, registered from a normal shell; it fires while this user is logged in (a locked screen
# is fine). `-LoggedOut` registers an S4U task that also fires with nobody logged in; that needs an ALREADY elevated
# shell. The installer never elevates itself (10's lesson: the UAC child registered nothing and the parent exited 0),
# and its last line counts the task.
param([ValidateSet('claude', 'codex')][string]$Provider, [switch]$LoggedOut, [switch]$Uninstall)
. (Join-Path $PSScriptRoot '../lib/UnidadTools.ps1')
$main = Get-MainCheckout $PSScriptRoot
$cfg = Get-RepoConfig $main
if (!$Provider) { $Provider = $cfg.loop.provider }
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole('Administrators')
if ($LoggedOut -and -not $elevated) { throw '-LoggedOut needs an administrator pwsh: Start-Process pwsh -Verb RunAs, then rerun' }
$parent = Split-Path $main -Parent
$clone = (Join-Path $parent "$($cfg.name)-loop") -replace '\\', '/'
Set-Location $main   # gh resolves the repo from the cwd

$task = "unidad-loop-$($cfg.name)"   # one task per repo: installing this loop never unregisters another repo's
Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
if ($Uninstall) { 'loop task removed'; exit 0 }

$origin = git -C $main remote get-url origin
if (-not (Test-Path (Join-Path $clone '.git'))) { git clone $origin $clone; if ($LASTEXITCODE -ne 0) { throw "git clone $origin failed" } }
# Claude ignores the settings of a folder it has never been trusted in: pre-trust the clone and every reviewer worktree.
$cfgFile = Join-Path $HOME '.claude.json'
$j = if (Test-Path $cfgFile) { Get-Content -Raw $cfgFile | ConvertFrom-Json -AsHashtable } else { @{} }
if (-not $j.projects) { $j.projects = @{} }
$trusted = @($clone) + @(1..$cfg.loop.reviewers | ForEach-Object { "$clone-r$_" })
foreach ($t in $trusted) {
    if (-not $j.projects[$t]) { $j.projects[$t] = @{} }
    $j.projects[$t].hasTrustDialogAccepted = $true
}
$j | ConvertTo-Json -Depth 100 | Set-Content -Encoding utf8 $cfgFile
gh label create 'loop:blocked' --force --description 'the loop could not merge this head; a new push lifts it' 2>&1 | Out-Null

# The Store pwsh's real path carries its version and vanishes on every auto-update; the WindowsApps alias survives.
$alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\pwsh.exe'
$pwsh = if (Test-Path $alias) { $alias } else { (Get-Command pwsh).Source }
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Minutes $cfg.loop.everyMinutes) -RepetitionDuration (New-TimeSpan -Days 3650)
# S4U runs without the keyring, so gh gets its token as a user env var.
# shortcut: the token lives in HKCU, readable by any process of this user; revoke with `gh auth refresh`.
[Environment]::SetEnvironmentVariable('GH_TOKEN', (gh auth token), 'User')
$logon = if ($LoggedOut) { 'S4U' } else { 'Interactive' }
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType $logon -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -WakeToRun -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 10) -MultipleInstances IgnoreNew
# The task runs the clone's copy of run.ps1: the clone is reset to origin/<main> each run, so the loop always runs the
# merged toolset, never a stale main checkout.
$runner = "$clone/tools/loop/run.ps1"
$action = New-ScheduledTaskAction -Execute $pwsh -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$runner`" -Provider $Provider -Clone `"$clone`" -Main `"$main`""
Register-ScheduledTask -TaskName $task -Action $action -Trigger $trigger -Principal $principal -Settings $settings -ErrorAction Stop | Out-Null

$have = @(Get-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue)
if ($have.Count -ne 1) { throw "expected the $task task after registration, found $($have.Count)" }
"installed: clone $clone | task $task (every $($cfg.loop.everyMinutes) min) | provider $Provider | logon $logon | next run $((Get-ScheduledTaskInfo -TaskName $task).NextRunTime)"
