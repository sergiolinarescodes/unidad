# Smoke tests of tools/live.ps1's pure parts: sync routing, the seat pool (stale PIDs included), the overlay and its
# exclude line, idle/cap expiry, the tour cut and the live manifest. Plain asserts, inputs inline, no Unity. Exit 1 on failure.
$ErrorActionPreference = 'Stop'
$tools = Split-Path $PSScriptRoot -Parent
. (Join-Path $tools 'live/Live.ps1')
$Temp = Join-Path ([IO.Path]::GetTempPath()) "live-smoke-$PID"
$failed = 0
function Assert($cond, $name) { if ($cond) { Write-Host "ok   $name" } else { Write-Host "FAIL $name"; $script:failed++ } }

try {
    New-Item -ItemType Directory -Force $Temp | Out-Null

    $replay = @('^Assets/StreamingAssets/Content/')
    Assert ((Get-LiveRoute 'Packages/com.x/Runtime/A.cs') -eq 'recompile' -and (Get-LiveRoute 'Assets/A/B.asmdef') -eq 'recompile' -and
            (Get-LiveRoute 'Assets/A/csc.rsp') -eq 'recompile' -and (Get-LiveRoute 'Sandbox/Packages/manifest.json') -eq 'recompile') 'Route_CodeAndPackagesRecompile'
    Assert ((Get-LiveRoute 'Assets/Ui/tokens.uss') -eq 'style' -and (Get-LiveRoute 'Assets\Ui\a.uxml') -eq 'style' -and (Get-LiveRoute 'R/Unidad.tss') -eq 'style') 'Route_StylesheetsStayInPlay'
    Assert ((Get-LiveRoute 'Assets/StreamingAssets/Content/Room/room.json' $replay) -eq 'replay' -and
            (Get-LiveRoute 'Assets/StreamingAssets/Content/Room/room.json') -eq 'asset' -and (Get-LiveRoute 'Assets/M/wood.mat' $replay) -eq 'asset') 'Route_ReplayPathsFromConfig'
    Assert ((Select-LiveRoute @('asset', 'style', 'recompile')) -eq 'recompile' -and (Select-LiveRoute @('asset', 'replay', 'style')) -eq 'replay' -and
            (Select-LiveRoute @('asset', 'style')) -eq 'style' -and (Select-LiveRoute @()) -eq 'none') 'Route_StrongestWins'

    # Search.settings: created, flipped or appended; other keys kept; the writer is idempotent.
    $off = 'indexOnEditorStartup = false'
    Assert ((Get-SearchSettingsText '') -eq "$off`n" -and (Get-SearchSettingsText '{}') -eq "$off`n") 'Search_EmptyOrBracesBecomesTheKey'
    $flipped = Get-SearchSettingsText "trackSelection = true`n  indexOnEditorStartup = true`nkeepOpen = true`n"
    Assert ($flipped -eq "trackSelection = true`n  $off`nkeepOpen = true`n") 'Search_TrueFlippedInPlace'
    Assert ((Get-SearchSettingsText "trackSelection = true`nscopes = {`n}`n") -eq "trackSelection = true`nscopes = {`n}`n$off`n") 'Search_MissingKeyAppended'
    $proj = Join-Path $Temp 'proj'
    Set-SearchIndexStartupOff $proj
    $first = [IO.File]::ReadAllText("$proj/UserSettings/Search.settings")
    Set-SearchIndexStartupOff $proj
    Assert ($first -eq "$off`n" -and [IO.File]::ReadAllText("$proj/UserSettings/Search.settings") -eq $first) 'Search_WriterCreatesAndIsIdempotent'

    $t0 = [DateTime]::new(2026, 10, 23, 8, 0, 0, [DateTimeKind]::Utc)
    Assert ($null -eq (Get-LiveExpiry $t0 $t0.AddMinutes(5) $t0.AddMinutes(14) 10 60)) 'Expiry_NoneWhileUsed'
    Assert ((Get-LiveExpiry $t0 $t0.AddMinutes(5) $t0.AddMinutes(15) 10 60) -eq 'idle') 'Expiry_IdleAfterTenQuietMinutes'
    Assert ((Get-LiveExpiry $t0 $t0.AddMinutes(59) $t0.AddMinutes(60) 10 60) -eq 'cap') 'Expiry_CapEvenWhenBusy'

    # Seats: two at most; a dead or reused PID frees its seat.
    $dir = Join-Path $Temp 'seats'
    $alive = @{ 101 = $t0; 102 = $t0; 104 = $t0 }
    $stale = { param($r) Test-LockStale $r { param($id) $alive[$id] } }
    $rec = { param($id) [ordered]@{ Pid = $id; ProcessStart = $t0.ToString('o'); Repo = 'r'; Branch = "b$id" } }
    $a = Enter-LiveSeat (& $rec 101) $dir 2 $stale
    $b = Enter-LiveSeat (& $rec 102) $dir 2 $stale
    $c = Enter-LiveSeat (& $rec 103) $dir 2 $stale
    Assert ($a -match 'seat1\.json$' -and $b -match 'seat2\.json$' -and $null -eq $c -and @(Get-LiveSeats $dir $stale).Count -eq 2) 'Seats_TwoAtMost'
    Set-Content (Join-Path $dir 'seat1.touch') 'x'
    $alive.Remove(101)
    $d = Enter-LiveSeat (& $rec 104) $dir 2 $stale
    Assert ($d -match 'seat1\.json$' -and (Read-LockRecord $d).Branch -eq 'b104' -and !(Test-Path (Join-Path $dir 'seat1.touch'))) 'Seats_DeadPidFreesItsSeatAndTouch'
    $alive[102] = $t0.AddSeconds(30)   # PID 102 reused by another program
    Assert (@(Get-LiveSeats $dir $stale | ForEach-Object { $_.Pid }) -join ',') -eq '104' 'Seats_ReusedPidIsStale'
    $s = Get-LiveSeats $dir $stale | Select-Object -First 1
    $s.Branch = 'renamed'; Write-LiveSeat $s
    Assert ((Read-LockRecord $s.File).Branch -eq 'renamed' -and !(Read-LockRecord $s.File).PSObject.Properties['File']) 'Seats_WriteKeepsFileOutOfRecord'

    # Overlay: copied in, excluded once, idempotent.
    $src = Join-Path $Temp 'overlay'; New-Item -ItemType Directory -Force "$src/Editor" | Out-Null
    Set-Content "$src/package.json" '{}'; Set-Content "$src/Editor/A.cs" 'class A {}'
    $slot = Join-Path $Temp 'slot'; $exclude = Join-Path $Temp 'git/info/exclude'
    New-Item -ItemType Directory -Force (Split-Path $exclude -Parent) | Out-Null
    Set-Content $exclude "# git ls-files --others --exclude-from=.git/info/exclude`n*.tmp"
    $cfg = @{ projectSubdir = 'Sandbox' }
    $rel = Get-LiveOverlayPath $cfg
    $line1 = Install-LiveOverlay $src $slot $rel $exclude
    Set-Content (Join-Path $slot "$rel/stale.txt") 'x'
    $line2 = Install-LiveOverlay $src $slot $rel $exclude
    $lines = @(Get-Content $exclude)
    Assert ($rel -eq 'Sandbox/Packages/com.unidad.live' -and (Get-LiveOverlayPath @{ projectSubdir = '.' }) -eq 'Packages/com.unidad.live') 'Overlay_PathUnderTheProject'
    Assert ($line1 -eq $line2 -and @($lines | Where-Object { $_ -eq '/Sandbox/Packages/com.unidad.live/' }).Count -eq 1 -and $lines -contains '*.tmp') 'Overlay_ExcludedOnce'
    Assert ((Test-Path (Join-Path $slot "$rel/Editor/A.cs")) -and !(Test-Path (Join-Path $slot "$rel/stale.txt"))) 'Overlay_ReinstallReplacesTheFolder'

    # Tour cut: up to the first line naming the label, then the hold lines.
    $tour = @('# zoom door', 'wait 1', 'act door.zoom { pointer.click world:door }', 'wait 2', 'act door.back { key Escape }')
    $cut = Get-LiveTourCut $tour 'door.zoom' @('rec off', 'wait 3600')
    Assert (($cut -join '|') -eq '# zoom door|wait 1|act door.zoom { pointer.click world:door }|rec off|wait 3600') 'Tour_CutAtLabelThenHold'
    Assert ((Get-LiveTourCut $tour '' @()).Count -eq 5) 'Tour_NoLabelPlaysAll'
    $threw = $false; try { Get-LiveTourCut $tour 'door' @() | Out-Null } catch { $threw = $true }
    Assert $threw 'Tour_UnknownLabelThrows'

    # Method reload: only body edits of [MethodReload] methods stay in Play; anything else recompiles.
    $cs = @'
using UnityEngine;
public sealed class View
{
    public Color Tint = Color.white;
    public int Count { get; set; }
#if UNIDAD_LIVE
    [global::Unity.Pipeline.MethodReload.MethodReload]
#endif
    public void Tick(float dt)
    {
        var label = $"n={Count} {{ok}}"; // a } in a comment
        Tint = new Color(0.95f, 0.55f, 0.15f); /* { */ var c = '}';
    }
    [Obsolete("x"), MethodReload(Id = "v.size")] public float Size() => Count * 2f;
    public void Other() { Count = 1; }
}
'@
    $edit = { param($From, $To) $cs.Replace($From, $To) }
    $body = Test-LiveMethodReload $cs (& $edit '0.95f, 0.55f, 0.15f' '0.2f, 0.8f, 0.3f')
    Assert ($body.Reload -and ($body.Methods -join ',') -eq 'Tick') 'Reload_BodyEditStaysInPlay'
    $expr = Test-LiveMethodReload $cs (& $edit 'Count * 2f;' 'Count * 3f + 1f;')
    Assert ($expr.Reload -and ($expr.Methods -join ',') -eq 'Size') 'Reload_ExpressionBodyEdit'
    Assert ((Get-LiveReloadBodies $cs).Count -eq 2 -and (Test-LiveMethodReload $cs ($cs -replace "`n", "`r`n")).Reload) 'Reload_BracesInStringsAndCommentsAndLineEndings'
    $sig = Test-LiveMethodReload $cs (& $edit 'Tick(float dt)' 'Tick(float dt, int n)')
    Assert (!$sig.Reload -and $sig.Reason -match 'Tick\(float dt, int n\)') 'Reload_SignatureChangeRecompiles'
    $member = Test-LiveMethodReload $cs (& $edit '    public void Other()' "    public int Added;`n    public void Other()")
    Assert (!$member.Reload -and $member.Reason -match 'public int Added;') 'Reload_NewMemberRecompiles'
    Assert (!(Test-LiveMethodReload $cs (& $edit 'Tint = Color.white;' 'Tint = Color.black;')).Reload) 'Reload_FieldChangeRecompiles'
    Assert (!(Test-LiveMethodReload $cs (& $edit 'Count = 1;' 'Count = 2;')).Reload) 'Reload_UnmarkedMethodRecompiles'
    Assert (!(Test-LiveMethodReload $cs (& $edit '    [global::Unity.Pipeline.MethodReload.MethodReload]' '')).Reload -and
            !(Test-LiveMethodReload 'class A { void B() { } }' 'class A { void B() { int x; } }').Reload) 'Reload_UnmarkingOrUnmarkedFileRecompiles'
    Assert (!(Test-LiveMethodReload $cs (& $edit 'var c = ' 'var c = {')).Reload) 'Reload_UnbalancedBracesRecompile'
    $r = { param($routes, $pending) $x = Select-LiveSyncRoute $routes $pending; "$($x.Route)$(if ($x.Why) { '!' })" }
    Assert ((& $r @('reload', 'reload') $false) -eq 'reload' -and (& $r @('reload', 'recompile') $false) -eq 'recompile' -and
            (& $r @('reload', 'style') $false) -eq 'recompile!' -and (& $r @('style') $true) -eq 'recompile!' -and
            (& $r @('style') $false) -eq 'style' -and (& $r @('reload') $true) -eq 'reload') 'Reload_SyncRouteRules'
    $asm = '{ "name": "G.View", "references": ["G.Logic"], "versionDefines": [] }'
    $patched = Add-LiveReloadAsmdef $asm
    $p = $patched | ConvertFrom-Json
    Assert (($p.references -join ',') -eq 'G.Logic,Unity.Pipeline,Unity.Pipeline.Attributes' -and $p.versionDefines[0].define -eq 'UNIDAD_LIVE' -and
            $p.versionDefines[0].name -eq 'com.unity.pipeline' -and (Add-LiveReloadAsmdef $patched) -ceq $patched) 'Reload_AsmdefPatchIdempotent'
    $rs = Join-Path $Temp 'rslot'
    New-Item -ItemType Directory -Force "$rs/Pkg/Samples~/S/View", "$rs/Proj/Assets/Samples/S/View", "$rs/Proj/Library/S" | Out-Null
    Set-Content "$rs/Pkg/Samples~/S/View/Host.cs" $cs; Set-Content "$rs/Proj/Assets/Samples/S/View/Host.cs" $cs; Set-Content "$rs/Proj/Library/S/Host.cs" $cs
    Set-Content "$rs/Proj/Assets/Samples/S/View/S.View.asmdef" $asm; Set-Content "$rs/Pkg/Samples~/S/View/S.View.asmdef" $asm
    Assert ((Resolve-LiveCompiledFile $rs "$rs/Proj" 'Pkg/Samples~/S/View/Host.cs') -eq ("$rs/Proj/Assets/Samples/S/View/Host.cs" -replace '\\', '/') -and
            (Resolve-LiveCompiledFile $rs "$rs/Proj" 'Proj/Assets/X.cs') -eq ("$rs/Proj/Assets/X.cs" -replace '\\', '/')) 'Reload_CompiledFileOfASampleCopy'
    $first = @(Install-LiveMethodReload $rs); $second = @(Install-LiveMethodReload $rs)
    Assert ($first.Count -eq 1 -and $first[0] -match 'Proj[\\/]Assets' -and $second.Count -eq 0 -and
            [IO.File]::ReadAllText("$rs/Pkg/Samples~/S/View/S.View.asmdef").Trim() -eq $asm) 'Reload_InstallPatchesVisibleAsmdefsOnce'

    # Manifest: header once with live=true, then one entry per frame; names count up.
    $frames = Join-Path $Temp 'run/frames'; New-Item -ItemType Directory -Force $frames | Out-Null
    Assert ((Get-NextFrameName $frames) -eq '00000.png') 'Frames_FirstName'
    Set-Content "$frames/00000.png" 'x'
    Add-LiveFrame $frames ([ordered]@{ branch = 'b' }) ([ordered]@{ frame = '00000.png'; label = 'room' })
    Set-Content "$frames/00001.png" 'x'
    Add-LiveFrame $frames ([ordered]@{ branch = 'b' }) ([ordered]@{ frame = '00001.png'; label = 'room.after' })
    $m = @(Get-Content "$frames/frames.jsonl" | ForEach-Object { $_ | ConvertFrom-Json })
    Assert ((Get-Content "$frames/frames.jsonl")[1] -eq '{"frame":"00000.png","label":"room"}') 'Frames_EntryKeepsKeyOrder'
    Assert ($m.Count -eq 3 -and $m[0].live -eq $true -and $m[0].branch -eq 'b' -and $m[2].label -eq 'room.after' -and (Get-NextFrameName $frames) -eq '00002.png') 'Frames_ManifestHeaderOnce'
} finally {
    Remove-Item -LiteralPath $Temp -Recurse -Force -ErrorAction SilentlyContinue
}
if ($failed) { Write-Host "$failed failed"; exit 1 }
Write-Host 'LiveSmoke: all green'
exit 0
