# Smoke tests of the toolset base: plain asserts, inputs inline, throwaway temp git repos. Exit 1 on any failure.
$ErrorActionPreference = 'Stop'
$tools = Split-Path $PSScriptRoot -Parent
. (Join-Path $tools 'lib/Lane.ps1')
$Temp = Join-Path ([IO.Path]::GetTempPath()) "tools-smoke-$PID"
$failed = 0
function Assert($cond, $name) { if ($cond) { Write-Host "ok   $name" } else { Write-Host "FAIL $name"; $script:failed++ } }
function Md5([string]$s) { -join ([Security.Cryptography.MD5]::HashData([Text.Encoding]::UTF8.GetBytes($s)) | ForEach-Object { $_.ToString('x2') }) }
function G { & git @args 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { throw "git $args failed" } }
function New-TempRepo([string]$Name, [string]$Roots, [string]$RepoJson) {
    $r = Join-Path $Temp $Name
    G init -q -b main $r
    G -C $r config user.email smoke@example.com; G -C $r config user.name smoke; G -C $r config core.autocrlf false
    New-Item -ItemType Directory -Force "$r/tools" | Out-Null
    Copy-Item -Recurse "$tools/meta-templates" "$r/tools/"
    Copy-Item "$tools/unity-metas.sh" "$r/tools/"
    [IO.File]::WriteAllText("$r/tools/meta-roots.txt", "$Roots`n")
    [IO.File]::WriteAllText("$r/tools/repo.json", $RepoJson)
    $r
}
function Metas([string]$Repo, [string[]]$MetasArgs) { Push-Location $Repo; try { & bash tools/unity-metas.sh @MetasArgs 2>&1 | Out-Null; $LASTEXITCODE } finally { Pop-Location } }
function Put([string]$Path, [string]$Text) { New-Item -ItemType Directory -Force (Split-Path $Path -Parent) | Out-Null; [IO.File]::WriteAllText($Path, $Text) }
$minimal = '{ "name": "unidad", "github": "o/unidad", "mainBranch": "main", "slotPrefix": "unidad-wt" }'

try {
    New-Item -ItemType Directory -Force $Temp | Out-Null
    Push-Location $tools
    $p = 'Assets/Cds/Cards/Logic/Foo.cs'
    Assert ((& bash unity-metas.sh --guid $p) -eq (Md5 $p).Substring(0, 32)) 'MetaGuid_IsMd5OfRepoRelativePath'
    $folder = 'Packages/com.unidad.core/Runtime'
    $r = (& bash unity-metas.sh --render folder $folder) -join "`n"
    Assert ($r -match 'folderAsset: yes' -and $r -match "guid: $(Md5 $folder)") 'MetaRender_FolderHasFolderAssetAndGuid'
    Pop-Location

    $repo = New-TempRepo 'metas' 'Assets' $minimal
    Put "$repo/Assets/A/B.cs" "class B { }`n"
    G -C $repo add Assets/A/B.cs
    $hook = Metas $repo @()
    $staged = @(git -C $repo diff --cached --name-only)
    $okHook = $hook -eq 0 -and $staged -contains 'Assets/A.meta' -and $staged -contains 'Assets/A/B.cs.meta'
    G -C $repo rm -q --cached Assets/A/B.cs
    Assert ($okHook -and (Metas $repo @('--check')) -eq 1) 'Metas_HookWritesMissing_CheckRefusesOrphan'

    $guarded = New-TempRepo 'guard' 'Assets' '{ "name": "g", "github": "o/g", "mainBranch": "main", "slotPrefix": "g", "renameGuard": ["Assets/Boot/**"] }'
    Put "$guarded/Assets/Boot/X.cs" "class X { }`n"
    G -C $guarded add Assets tools
    $null = Metas $guarded @()
    G -C $guarded commit -q -m base
    New-Item -ItemType Directory -Force "$guarded/Assets/Other" | Out-Null
    G -C $guarded mv Assets/Boot/X.cs Assets/Other/X.cs
    G -C $guarded mv Assets/Boot/X.cs.meta Assets/Other/X.cs.meta
    Push-Location $guarded; $out = (& bash tools/unity-metas.sh 2>&1) -join "`n"; $code = $LASTEXITCODE; Pop-Location
    Assert ($code -eq 1 -and $out -match 'Assets/Boot/X\.cs -> Assets/Other/X\.cs renames') 'Metas_RenameUnderGuardRefused'

    $csproj = '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup></ItemGroup></Project>'
    $repoNames = @('Unidad.Core', 'Unidad.Flow')
    $probs = Compare-AsmdefCsproj '{ "name": "Unidad.Flow", "references": ["Unidad.Core"] }' $csproj '-langversion:14' $true $repoNames
    Assert ($probs.Count -eq 1 -and $probs[0] -match 'Unidad\.Core') 'Parity_FlagsMissingProjectReference'
    $probs = Compare-AsmdefCsproj '{ "name": "Unidad.Flow", "allowUnsafeCode": true }' $csproj '-langversion:9' $true $repoNames
    Assert ($probs.Count -eq 2) 'Parity_FlagsUnsafeAndLangVersion'
    $probs = Compare-AsmdefCsproj '{ "name": "Unidad.Flow", "references": ["Unity.InputSystem"] }' $csproj '-langversion:14' $true $repoNames
    Assert ($probs.Count -eq 0) 'Parity_IgnoresUnityPackageReferences'

    $cfg = Get-RepoConfig -Json $minimal
    Assert ($cfg.codeSlots -eq 8 -and (Get-SlotPath $cfg 'C:/r' 'slot3') -eq 'C:/r/unidad-wt-slot3' -and (Get-SlotPath $cfg 'C:/r' 'unity1') -eq 'C:/r/unidad-wt-unity1') 'RepoConfig_DefaultsAndSlotPaths'

    $calls = 0
    $red = Find-FirstRed 7 { param($i) $script:calls++; $i -lt 5 }
    Assert ($red -eq 5 -and $calls -le 3) 'FindFirstRed_BisectsInLogCalls'

    $map = @(@{ match = '^Assets/Cds/([^/]+)/'; project = 'dotnet/Cds.$1.Smoke' })
    $paths = 'Assets/Cds/Cards/Logic/A.cs', 'Assets/Cds/Cards/Tests~/B.cs', 'Assets/Cds/Room/View/C.cs'
    $mapped = Get-MappedProjects $paths $map
    $lanePaths = '/View/', '^Assets/Cds/Sim/Burst/', '\.asmdef$'
    Assert (($mapped -join ',') -eq 'dotnet/Cds.Cards.Smoke,dotnet/Cds.Room.Smoke' -and !(Test-MatchesAny @($paths[0]) $lanePaths) -and (Test-MatchesAny @($paths[2]) $lanePaths)) 'MappedProjects_SubstituteAndDedupe'

    $treeCfg = Get-RepoConfig -Json '{ "name": "g", "github": "o/g", "mainBranch": "master", "slotPrefix": "g",
        "binaryAllow": ["Packages/com.unidad.core/Analyzers/Unidad.Analyzers.dll"],
        "tree": { "forbidden": [{ "glob": "*.shader", "regex": "shader_feature", "why": "D-29" }], "frameworkPaths": "^Packages/com\\.unidad\\." } }'
    $files = 'Assets/x.dll', 'Assets/Look/a.shader', 'Packages/com.unidad.core/Analyzers/Unidad.Analyzers.dll'
    $texts = @{ 'Assets/Look/a.shader' = '#pragma shader_feature _A' }
    $probs = Test-TreeFiles $files { param($f) $texts[$f] } $treeCfg @('Packages/com.unidad.core/Runtime/A.cs') '+| #11 | x | y |' 12
    $ok = $probs.Count -eq 3 -and ($probs -match 'binary not allowed: Assets/x\.dll') -and ($probs -match 'a\.shader') -and ($probs -match '\| #12 \|')
    $probs = Test-TreeFiles @('Packages/com.unidad.core/Analyzers/Unidad.Analyzers.dll') { '' } $treeCfg @('Packages/com.unidad.core/Runtime/A.cs') '+| #12 | Packages/com.unidad.core | fix |' 12
    Assert ($ok -and $probs.Count -eq 0) 'TreeCheck_RefusesBinaryShaderFeatureAndMissingRow'

    $env:UNIDAD_TOOLS_HOME = Join-Path $Temp 'home'
    $inner = Invoke-WithEditorLock -Owner 'smoke-a' -TimeoutMinutes 1 -WaitMinutes 0.1 -ScriptBlock {
        Invoke-WithEditorLock -Owner 'smoke-a' -TimeoutMinutes 1 -WaitMinutes 0.05 -ScriptBlock { 'inner' }
    }
    # An older live ticket (our parent process) is ahead of us: FIFO makes us wait, and time out.
    $parentPid = (Get-Process -Id $PID).Parent.Id
    $older = Join-Path (Get-EditorLockDir) 'queue/0000000000000000001-1.json'
    @{ Pid = $parentPid; ProcessStart = (Get-ProcessStartUtc $parentPid).ToString('o'); Owner = 'smoke-b' } | ConvertTo-Json | Set-Content $older
    $waited = $false
    try { Invoke-WithEditorLock -Owner 'smoke-c' -WaitMinutes 0.05 -ScriptBlock { } } catch [TimeoutException] { $waited = $true }
    Remove-Item $older
    $after = Invoke-WithEditorLock -Owner 'smoke-c' -WaitMinutes 0.1 -ScriptBlock { 'got it' }
    Assert ($inner -eq 'inner' -and $waited -and $after -eq 'got it' -and !(Test-Path (Join-Path (Get-EditorLockDir) 'unity-editor.lock'))) 'EditorLock_ReentrantPerOwner_FifoAcrossOwners'

    $tb = Get-RepoConfig -Json '{ "name": "g", "github": "o/g", "mainBranch": "master", "slotPrefix": "g",
        "lane": { "playerBootArgs": ["--set", "frames=60", "--set", "fresh=true", "--set", "report={report}"] } }'
    $windowed = (ConvertTo-TestbedLine $tb 'room' '' 0) -join ' '
    $frames = (ConvertTo-TestbedLine $tb 'room' 'fresh=false' 60) -join ' '
    Assert ($windowed -eq '--set start=room' -and $frames -match '--set report=\S+/g-report\.txt' -and $frames -match '--set frames=60' -and
            $frames -match '--set fresh=false') 'Testbed_ReportOnlyWithFrames_SetOverridesBase'
    Assert ((ConvertTo-TestbedLine $tb 'room' '' 60) -contains 'fresh=true') 'Testbed_FramesRunIsFresh'
} catch {
    Write-Host "FAIL (threw) $($_.Exception.Message) at $($_.InvocationInfo.ScriptLineNumber)"; $failed++
} finally {
    Remove-Item Env:UNIDAD_TOOLS_HOME -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $Temp -ErrorAction SilentlyContinue
}
if ($failed) { Write-Host "$failed failed"; exit 1 }
Write-Host 'all green'
exit 0
