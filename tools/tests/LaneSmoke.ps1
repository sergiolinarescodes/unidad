# Smoke tests of the serial lane, refpack and testbed cores: plain asserts, inputs inline, throwaway temp git repos.
# Exit 1 on any failure.
$ErrorActionPreference = 'Stop'
$tools = Split-Path $PSScriptRoot -Parent
. (Join-Path $tools 'lib/Lane.ps1')
$Temp = Join-Path ([IO.Path]::GetTempPath()) "lane-smoke-$PID"
$failed = 0
function Assert($cond, $name) { if ($cond) { Write-Host "ok   $name" } else { Write-Host "FAIL $name"; $script:failed++ } }
function G { & git @args 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { throw "git $args failed" } }
function Put([string]$Path, [string]$Text) { New-Item -ItemType Directory -Force (Split-Path $Path -Parent) | Out-Null; [IO.File]::WriteAllText($Path, $Text) }
function New-Repo([string]$Name) {
    $r = Join-Path $Temp $Name
    G init -q -b main $r
    G -C $r config user.email smoke@example.com; G -C $r config user.name smoke; G -C $r config core.autocrlf false
    Put "$r/README" "x`n"; G -C $r add README; G -C $r commit -q -m base
    $r
}
function Commit([string]$Repo, [string]$File) { Put "$Repo/$File" "$File`n"; G -C $Repo add $File; G -C $Repo commit -q -m $File; (git -C $Repo rev-parse HEAD).Trim() }
$cfg = Get-RepoConfig -Json '{ "name": "unidad", "github": "o/unidad", "mainBranch": "main", "slotPrefix": "unidad-wt" }'

try {
    New-Item -ItemType Directory -Force $Temp | Out-Null
    $t0 = [DateTime]::new(2026, 10, 20, 8, 0, 0, [DateTimeKind]::Utc)
    $starts = @{ 2 = $t0 }
    $get = { param($id) $starts[$id] }
    Assert ((Test-LockStale @{ Pid = 1; ProcessStart = $t0.ToString('o') } $get) -and
            (Test-LockStale @{ Pid = 2; ProcessStart = $t0.AddSeconds(3).ToString('o') } $get) -and
            !(Test-LockStale @{ Pid = 2; ProcessStart = $t0.ToString('o') } $get)) 'LockStale_DeadOrReusedPid'

    $tickets = @([pscustomobject]@{ Name = '0003-c' }, [pscustomobject]@{ Name = '0001-a' }, [pscustomobject]@{ Name = '0002-b' })
    Assert ((Get-QueueHead $tickets { param($t) $t.Name -eq '0001-a' }).Name -eq '0002-b') 'QueueHead_OldestLiveTicketWins'

    $regen = Get-UnityArgs 'regenerate' 'C:/s/Sandbox' 'C:/s/l.log'
    $bee = Get-UnityArgs 'bee' 'C:/s/Sandbox'
    $player = Get-UnityArgs 'player' 'C:/s/Sandbox' -Method 'A.B.Player' -BuildOut 'C:/s/Sandbox/Build'
    $exec = Get-UnityArgs 'exec' 'C:/s/Sandbox' -Method 'X.Lane.Setup' -Extra @('-k', 'v')
    $ok = !(@('-batchmode', '-nographics', '-enable-msbuild', '-quit', '-projectPath', '-logFile') | Where-Object { $regen -notcontains $_ })
    $ok = $ok -and $bee -notcontains '-enable-msbuild' -and $bee -contains '-quit'
    $ok = $ok -and ($player -join ' ') -match '-buildTarget win64 -executeMethod A\.B\.Player -buildOut C:/s/Sandbox/Build -development' -and $player -notcontains '-enable-msbuild'
    Assert ($ok -and ($exec -join ' ') -match '-enable-msbuild .*-executeMethod X\.Lane\.Setup -k v$') 'UnityArgs_PerMode'

    $run = Get-PlayerRunArgs (ConvertTo-LaunchLine -Start 'room' -Set 'seed=7' -Frames 30 -Base @('--set', 'frames=60') -Extra @{ strict = 'true' })
    $ok = ($run -join ' ') -eq '-batchmode -nographics -- --set frames=30 --set start=room --set seed=7 --set strict=true'
    $sha = 'a' * 40
    Assert ($ok -and (Test-BuildReusable "$sha`n" $sha $true) -and !(Test-BuildReusable ('b' * 40) $sha $true) -and !(Test-BuildReusable $sha $sha $false)) 'PlayerRun_LaunchLineAndStampedBuildReuse'

    Assert ((Resolve-ExecMethod 'Setup' 'Unidad.Sandbox.Editor.Lane.') -eq 'Unidad.Sandbox.Editor.Lane.Setup' -and (Resolve-ExecMethod 'A.B.C' 'P.') -eq 'A.B.C') 'ExecMethod_PrefixRule'

    $log = @(
        'Assets/A.cs(3,5): error CS0103: The name ''x'' does not exist',
        'Assets/A.cs(3,5): error CS0103: The name ''x'' does not exist',
        'Packages/B.cs(1,1): error UD015: mutable static',
        'Assets/C.cs(2,2): warning CS0168: declared but never used',
        'Build FAILED.')
    Assert ((Get-CompileErrors $log).Count -eq 2) 'CompileErrors_UniqueCsAndUd'
    Assert ((Get-CompileErrors @('Packages/D.cs(45,29): error UAC0031: Lifecycle method must be declared in a partial class')).Count -eq 1) 'CompileErrors_UnityAnalyzerUac'

    $repo = New-Repo 'hash'
    Put "$repo/Assets/A.cs" "class A { }`n"; Put "$repo/Assets/A.asmdef" '{ "name": "A" }'; G -C $repo add Assets
    $before = Get-CheckoutListHash $repo $false
    $beforeGlob = Get-CheckoutListHash $repo $true
    Put "$repo/Assets/B.cs" "class B { int x = }`n"; G -C $repo add Assets/B.cs
    $after = Get-CheckoutListHash $repo $false
    $ok = $before -ne $after -and $beforeGlob -eq (Get-CheckoutListHash $repo $true)
    Assert ($ok -and (Test-RegenerateNeeded @{ GeneratedListHash = $before } $after $true) -and !(Test-RegenerateNeeded @{ GeneratedListHash = $after } $after $true) -and
            (Test-RegenerateNeeded @{ GeneratedListHash = $after } $after $false)) 'Regenerate_NewCsChangesTheListHash'

    Put "$repo/Assets/A.asmdef" '{ "name": "A", "references": [ "B" ] }'; G -C $repo add Assets/A.asmdef
    Assert ((Get-CheckoutListHash $repo $true) -ne $beforeGlob) 'Regenerate_AsmdefReferenceChangeChangesTheListHash'

    $script:regenerated = 0
    $fakeBuild = {   # a fake MSBuild-mode build: a CS error for every .cs with a dangling '= }'
        $lines = @(Get-ChildItem -Recurse "$repo/Assets" -Filter *.cs | Where-Object { (Get-Content -Raw $_.FullName) -match '= \}' } |
            ForEach-Object { "$($_.Name)(1,19): error CS1525: Invalid expression term '}'" })
        @{ Code = $(if ($lines) { 1 } else { 0 }); Lines = $lines + 'Build FAILED.' }
    }
    $r = Invoke-CompileCore @{ GeneratedListHash = $before } $after { $true } { $script:regenerated++; 0 } $fakeBuild
    Assert ($r.Code -eq 1 -and $r.Regenerated -and $script:regenerated -eq 1 -and $r.Errors.Count -eq 1 -and $r.Errors[0] -match 'B\.cs.*CS1525') 'Compile_NewCsWithErrorExitsRed'

    $origin = New-Repo 'origin'
    G -C $origin checkout -q -b feat; $originSha = Commit $origin 'f1'; G -C $origin checkout -q main
    $clone = Join-Path $Temp 'clone'
    G clone -q $origin $clone
    G -C $clone config user.email smoke@example.com; G -C $clone config user.name smoke
    $viaOrigin = Resolve-LaneCommit $clone 'feat'
    G -C $clone checkout -q -b feat origin/feat; $localSha = Commit $clone 'f2'
    $viaLocal = Resolve-LaneCommit $clone 'feat'
    Assert ($viaOrigin.Source -eq 'origin' -and $viaOrigin.Sha -eq $originSha -and $viaLocal.Source -eq 'local' -and $viaLocal.Sha -eq $localSha -and !(Resolve-LaneCommit $clone 'nope')) 'LaneCommit_LocalRefBeatsOrigin'

    $pick = Select-RefpackFiles @('UnityEngine.CoreModule.dll', 'UnityEditor.dll', 'Unity.InputSystem.dll', 'Unity.InputSystem.Editor.dll',
                                  'Unidad.Core.dll', 'Unidad.Adapter.dll', 'Assembly-CSharp.dll') $cfg
    $ok = ($pick.Runtime -join ',') -eq 'UnityEngine.CoreModule.dll,Unity.InputSystem.dll' -and ($pick.Editor -join ',') -eq 'UnityEditor.dll,Unity.InputSystem.Editor.dll'
    $packs = Join-Path $Temp 'refpacks/unidad'
    Write-RefpackPointer $packs 'C:\p\one'; Write-RefpackPointer $packs 'C:\p\two'
    Assert ($ok -and [IO.File]::ReadAllText("$packs/current.txt") -eq 'C:/p/two' -and !(Test-Path "$packs/current.txt.tmp")) 'Refpack_SelectsAndSwapsPointer'

    $line = ConvertTo-LaunchLine -Set 'fresh=true;grant=cards:2,skillsets:6'
    $threw = $false; try { ConvertTo-LaunchLine -Set 'oops' | Out-Null } catch { $threw = $true }
    Assert (($line -join '|') -eq '--set|fresh=true|--set|grant=cards:2,skillsets:6' -and $threw) 'LaunchLine_OneSetStringTwoPairs'

    $porcelain = '?? Sandbox/Assets/X.asset', '?? Sandbox/Library/a', ' M Sandbox/ProjectSettings/P.asset', '?? Sandbox/Assets/Y.gen.csproj',
                 ' D Sandbox/Assets/Z.mat', '?? tools/meta-templates/cs.meta', '?? Sandbox/Assets/Temp/t.txt'
    $execPick = Select-CollectPaths $porcelain (Resolve-CollectRoots @('Assets', '../tools/meta-templates') 'Sandbox')
    $importPick = Select-CollectPaths $porcelain (Resolve-CollectRoots @($cfg.lane.collectRoots) 'Sandbox')
    $ok = ($execPick.Path -join ',') -eq 'Sandbox/Assets/X.asset,Sandbox/Assets/Z.mat,tools/meta-templates/cs.meta' -and ($execPick | Where-Object Deleted).Path -eq 'Sandbox/Assets/Z.mat'
    Assert ($ok -and ($importPick.Path -join ',') -eq 'Sandbox/Assets/X.asset,Sandbox/Assets/Z.mat') 'Collect_OnlyUnderRootsForExecAndImport'

    G -C $origin checkout -q -b merged; $null = Commit $origin 'm1'; G -C $origin checkout -q main; G -C $origin merge -q --no-ff -m 'merge' merged
    G -C $origin checkout -q -b open; $openSha = Commit $origin 'o1'; G -C $origin checkout -q main
    G -C $clone fetch -q origin
    $mainSha = (git -C $clone rev-parse origin/main).Trim()
    $open = Resolve-TestbedTarget $clone 'open' 'main' $null
    $merged = Resolve-TestbedTarget $clone 'merged' 'main' $null
    $gone = Resolve-TestbedTarget $clone 'gone' 'main' $null
    Assert ($open.Kind -eq 'merge' -and $open.Sha -eq $mainSha -and $open.Head -eq $openSha -and
            $merged.Kind -eq 'default' -and $merged.Sha -eq $mainSha -and $merged.Note -match 'merged at' -and
            $gone.Kind -eq 'default' -and $gone.Sha -eq $mainSha) 'Testbed_OpenMergesMergedOrMissingShowsDefault'

    $now = [DateTime]::UtcNow
    Assert ((Test-ClaimIdle @{ ClaimedBy = 'b'; LastUsed = $now.AddMinutes(-21).ToString('o') } $now) -and
            !(Test-ClaimIdle @{ ClaimedBy = 'b'; LastUsed = $now.AddMinutes(-5).ToString('o') } $now)) 'Claim_IdleReleaseAfterTimeout'
} catch {
    Write-Host "FAIL (threw) $($_.Exception.Message) at $($_.InvocationInfo.ScriptLineNumber)"; $failed++
} finally {
    Remove-Item -Recurse -Force $Temp -ErrorAction SilentlyContinue
}
if ($failed) { Write-Host "$failed failed"; exit 1 }
Write-Host 'all green'
exit 0
