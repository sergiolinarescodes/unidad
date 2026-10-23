# The seed run (U-58): one throwaway batch editor that captures the importer meta templates and the first refpack.
#   pwsh -File tools/seed-unity.ps1 [-UnityPath <Unity.exe>] [-CapMinutes 15] [-Repos unidad,11-unidad-unity]
# Under the editor lock it builds a fresh project in <home>/seed/project with the games' manifest (inputsystem, URP,
# ugui, glTFast, burst, collections), drops one file of every type the repos commit, runs ONE batch import + compile
# (-quit; the probe's editor script creates the .unity/.prefab/.mat/.asset and exits), then:
#   - copies each importer meta into tools/meta-templates/<key>.meta of THIS checkout (guid -> {{GUID}}, no timeCreated);
#   - copies the package DLLs of Library/ScriptAssemblies into <home>/refpacks/seed/ (editor ones into Editor/), never
#     Unidad.* or the probe's own (U-59: engine DLLs come from Data/Managed directly);
#   - points <home>/refpacks/<repo>/current.txt at it for each -Repos entry, unless it already names a newer refpack.
# Rerun after an editor upgrade (U-47). Exit 0 ok, 1 the batch run failed (its log path is printed), 3 lock wait timed
# out, 124 killed at the cap. Kills nothing but the Unity PID it started.
# -Fbx / -Ogg: sample files for the two types no script can write (default: the first small one under a sibling repo's assets/).
param([string]$UnityPath, [int]$CapMinutes = 15, [string[]]$Repos = @('unidad', '11-unidad-unity'), [double]$WaitMinutes = 60,
      [string]$Fbx, [string]$Ogg)
. (Join-Path $PSScriptRoot 'lib/EditorLock.ps1')
$root = Get-RepoRoot $PSScriptRoot
$cfg = Get-RepoConfig $root
$editor = Resolve-UnityEditor $null $UnityPath $cfg.editorVersion
$dotnet = Resolve-UnityDotnet $editor
$toolsHome = Get-ToolsHome
$seed = "$toolsHome/seed"
$proj = "$seed/project"
$templates = Join-Path $root 'tools/meta-templates'

# --- 1. A fresh project with one probe of every committed type.
if (Test-Path -LiteralPath $proj) { Remove-Item -LiteralPath $proj -Recurse -Force }
$probe = "$proj/Assets/_MetaProbe"
Set-SearchIndexStartupOff $proj
New-Item -ItemType Directory -Force "$probe/Code", "$probe/Ref", "$proj/Assets/StreamingAssets/_MetaProbe", "$proj/Assets/Editor", "$proj/Packages" | Out-Null
function Put([string]$Path, [string]$Text) { [IO.File]::WriteAllText($Path, $Text.Replace("`r`n", "`n")) }
Put "$proj/Packages/manifest.json" @'
{
  "dependencies": {
    "com.unity.inputsystem": "6.7.0",
    "com.unity.render-pipelines.universal": "17.7.0",
    "com.unity.ugui": "7.0.0",
    "com.unity.cloud.gltfast": "6.20.0",
    "com.unity.burst": "2.0.0",
    "com.unity.collections": "6.7.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.imageconversion": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.physics": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0"
  }
}
'@
Put "$probe/Code/a.cs" "namespace MetaProbe { internal static class A { } }`n"
Put "$probe/Code/a.asmdef" '{ "name": "MetaProbe", "noEngineReferences": true }'
Put "$probe/Code/csc.rsp" "-warnaserror-`n"
Put "$probe/Ref/a.asmref" '{ "reference": "MetaProbe" }'
Put "$probe/a.json" '{ "a": 1 }'
Put "$proj/Assets/StreamingAssets/_MetaProbe/a.json" '{ "a": 1 }'
Put "$probe/a.md" "# a`n"
Put "$probe/a.txt" "a`n"
Put "$probe/LICENSE" "probe`n"
Put "$probe/a.uxml" "<ui:UXML xmlns:ui=`"UnityEngine.UIElements`"></ui:UXML>`n"
Put "$probe/a.uss" ".a { }`n"
Put "$probe/a.tss" "@import url(`"unity-theme://default`");`n"
Put "$probe/a.shader" "Shader `"MetaProbe/A`" { SubShader { Pass { } } }`n"
Put "$probe/a.hlsl" "float4 MetaProbeA() { return 0; }`n"
Put "$probe/a.cginc" "float4 MetaProbeB() { return 0; }`n"
Put "$probe/a.compute" "#pragma kernel CSMain`n[numthreads(1,1,1)] void CSMain(uint3 id : SV_DispatchThreadID) { }`n"
[IO.File]::WriteAllBytes("$probe/a.png", [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=='))
$samples = 800; $wav = [IO.MemoryStream]::new(); $w = [IO.BinaryWriter]::new($wav)
$w.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $w.Write([int](36 + $samples * 2)); $w.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
$w.Write([int]16); $w.Write([int16]1); $w.Write([int16]1); $w.Write([int]8000); $w.Write([int]16000); $w.Write([int16]2); $w.Write([int16]16)
$w.Write([Text.Encoding]::ASCII.GetBytes('data')); $w.Write([int]($samples * 2)); 1..$samples | ForEach-Object { $w.Write([int16]0) }
[IO.File]::WriteAllBytes("$probe/a.wav", $wav.ToArray())
Copy-Item 'C:/Windows/Fonts/arial.ttf' "$probe/a.ttf"
Copy-Item 'C:/Windows/Fonts/arial.ttf' "$probe/a.otf"   # an OpenType container with TrueType outlines: same importer
function Find-Sample([string]$Given, [string]$Filter) {
    if ($Given) { return $Given }
    Get-ChildItem -Path (Join-Path (Split-Path $root -Parent) '*/assets') -Recurse -Filter $Filter -ErrorAction SilentlyContinue |
        Where-Object Length -lt 200kb | Select-Object -First 1 -ExpandProperty FullName
}
foreach ($t in @(@('ogg', $Ogg), @('fbx', $Fbx))) {
    $src = Find-Sample $t[1] "*.$($t[0])"
    if ($src) { Copy-Item -LiteralPath $src "$probe/a.$($t[0])" } else { Write-Warning "no .$($t[0]) sample found; its template is not captured (pass -$($t[0]))" }
}
# A managed plugin DLL of our own (copying an engine or BCL DLL would clash with the real one).
$lib = "$seed/plugin"
New-Item -ItemType Directory -Force $lib | Out-Null
Put "$lib/MetaProbePlugin.csproj" '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><AssemblyName>MetaProbePlugin</AssemblyName></PropertyGroup></Project>'
Put "$lib/P.cs" "namespace MetaProbePlugin { public static class P { } }`n"
& $dotnet build "$lib/MetaProbePlugin.csproj" -c Release -o "$lib/out" -v:q -nologo | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Host 'seed: the probe plugin did not build'; exit 1 }
Copy-Item "$lib/out/MetaProbePlugin.dll" "$probe/a.dll"
Put "$proj/Assets/Editor/SeedProbe.cs" @'
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SeedProbe
{
    public static void Run()
    {
        try
        {
            const string dir = "Assets/_MetaProbe/";
            // Scenes first: NewScene unloads unreferenced assets.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, dir + "a.unity");
            var go = new GameObject("a");
            PrefabUtility.SaveAsPrefabAsset(go, dir + "a.prefab");
            Object.DestroyImmediate(go);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Hidden/InternalErrorShader");
            AssetDatabase.CreateAsset(new Material(shader), dir + "a.mat");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>(), dir + "a.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[seed] error: " + e);
            EditorApplication.Exit(1);
        }
    }
}
'@

# --- 2. One batch import + compile, under the machine-wide editor lock.
$logFile = "$seed/seed.log"
try {
    $code = Invoke-WithEditorLock -Owner "seed:$PID" -TimeoutMinutes $CapMinutes -WaitMinutes $WaitMinutes -ScriptBlock {
        $psi = [Diagnostics.ProcessStartInfo]::new($editor)
        foreach ($a in '-batchmode', '-nographics', '-quit', '-projectPath', $proj, '-executeMethod', 'SeedProbe.Run', '-logFile', $logFile) { $psi.ArgumentList.Add($a) }
        $psi.UseShellExecute = $false
        $p = [Diagnostics.Process]::Start($psi)
        Write-Host "seed: Unity PID $($p.Id) importing $proj (cap $CapMinutes min)"
        Wait-OwnProcess $p $CapMinutes
    }
} catch [TimeoutException] { Write-Host "seed: $($_.Exception.Message)"; exit 3 }
if ($code -ne 0) { Write-Host "seed: the batch run exited $code; see $logFile"; exit $(if ($code -eq 124) { 124 } else { 1 }) }

# --- 3. Meta templates.
$keys = [ordered]@{
    folder = "$probe/Code"; cs = "$probe/Code/a.cs"; asmdef = "$probe/Code/a.asmdef"; asmref = "$probe/Ref/a.asmref"
    text = "$probe/a.json"; default = "$proj/Assets/StreamingAssets/_MetaProbe/a.json"
    uxml = "$probe/a.uxml"; uss = "$probe/a.uss"; tss = "$probe/a.tss"
    shader = "$probe/a.shader"; hlsl = "$probe/a.hlsl"; cginc = "$probe/a.cginc"; compute = "$probe/a.compute"
    png = "$probe/a.png"; wav = "$probe/a.wav"; ogg = "$probe/a.ogg"; ttf = "$probe/a.ttf"; otf = "$probe/a.otf"; fbx = "$probe/a.fbx"
    unity = "$probe/a.unity"; prefab = "$probe/a.prefab"; mat = "$probe/a.mat"; asset = "$probe/a.asset"; dll = "$probe/a.dll"
}
$pkgMeta = @(Get-ChildItem "$proj/Library/PackageCache" -Directory -Filter 'com.unity.inputsystem*' -ErrorAction SilentlyContinue | ForEach-Object { "$($_.FullName)/package.json" }) +
    "$(Split-Path $editor -Parent)/Data/Resources/PackageManager/BuiltInPackages/com.unity.inputsystem/package.json"
$keys['package'] = $pkgMeta | Where-Object { Test-Path -LiteralPath "$_.meta" } | Select-Object -First 1
$captured = @(); $missing = @()
foreach ($k in $keys.Keys) {
    $src = if ($keys[$k]) { "$($keys[$k]).meta" }
    if (!$src -or !(Test-Path -LiteralPath $src)) { $missing += $k; continue }
    $text = (Get-Content -Raw -LiteralPath $src).Replace("`r`n", "`n")
    $text = [regex]::Replace($text, '(?m)^guid: [0-9a-f]{32}$', 'guid: {{GUID}}')
    $text = [regex]::Replace($text, '(?m)^timeCreated: .*\n', '')
    # A font's family name is the probe's (Arial), not the next font's: Unity reads it from the file.
    $text = [regex]::Replace($text, '(?m)^  fontNames:\n(  - .*\n)+', "  fontNames: []`n")
    [IO.File]::WriteAllText((Join-Path $templates "$k.meta"), $text)
    $captured += $k
}
Write-Host "seed: templates captured: $($captured -join ' ')"
if ($missing) { Write-Warning "seed: no meta for: $($missing -join ' ') (kept any template already there)" }

# --- 4. The seed refpack and the pointers.
$pack = "$toolsHome/refpacks/seed"
if (Test-Path -LiteralPath $pack) { Remove-Item -LiteralPath $pack -Recurse -Force }
New-Item -ItemType Directory -Force "$pack/Editor" | Out-Null
$copied = 0
foreach ($dll in Get-ChildItem "$proj/Library/ScriptAssemblies" -Filter '*.dll' -ErrorAction SilentlyContinue) {
    $n = $dll.Name
    if ($n -match '^(Unidad\.|MetaProbe|Assembly-CSharp)') { continue }
    if (Test-MatchesAny @($n) @($cfg.refpack.editorInclude)) { Copy-Item $dll.FullName "$pack/Editor/"; $copied++ }
    elseif (Test-MatchesAny @($n) @($cfg.refpack.include)) { Copy-Item $dll.FullName "$pack/"; $copied++ }
}
if ($copied -eq 0) { Write-Host "seed: Library/ScriptAssemblies held no package DLL; see $logFile"; exit 1 }
Write-Host "seed: refpack $pack ($copied DLLs)"
foreach ($r in $Repos) {
    $dir = "$toolsHome/refpacks/$r"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $current = Get-RefpackPointer $r
    if ($current -and ($current -replace '\\', '/') -ne $pack) { Write-Host "seed: $r keeps its newer refpack $current"; continue }
    [IO.File]::WriteAllText("$dir/current.txt.tmp", $pack)
    Move-Item -Force "$dir/current.txt.tmp" "$dir/current.txt"
    Write-Host "seed: $r/current.txt -> $pack"
}
exit 0
