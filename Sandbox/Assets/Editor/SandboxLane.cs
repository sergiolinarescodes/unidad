using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Unidad.Sandbox.Editor
{
    /// <summary>The Sandbox's lane recipes (<c>unity-lane.ps1 -Exec &lt;Method&gt;</c>). Each exits through <c>EditorApplication.Exit</c>.</summary>
    public static class Lane
    {
        public static void Setup() => Run(nameof(Setup), () =>
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length == 0) throw new InvalidOperationException("ProjectSettings/ProjectSettings.asset not loaded");
            var so = new SerializedObject(settings[0]);
            var input = so.FindProperty("activeInputHandler");
            if (input == null) throw new InvalidOperationException("activeInputHandler not found");
            input.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
            AssetDatabase.SaveAssets();
        });

        // Probes one file per committed type, imports them, and writes each importer meta as tools/meta-templates/<key>.meta
        // (guid -> {{GUID}}, no timeCreated). Collect with: -Exec CaptureMetaTemplates -Collect ../tools/meta-templates
        public static void CaptureMetaTemplates() => Run(nameof(CaptureMetaTemplates), () =>
        {
            const string probe = "Assets/_MetaProbe";
            const string streaming = "Assets/StreamingAssets/_MetaProbe";
            var files = new (string Key, string Path, string Text)[]
            {
                ("cs", probe + "/Code/a.cs", "namespace MetaProbe { internal static class A { } }\n"),
                ("asmdef", probe + "/Code/a.asmdef", "{ \"name\": \"MetaProbe\", \"noEngineReferences\": true }\n"),
                ("default", streaming + "/a.json", "{ \"a\": 1 }\n"),
                ("asmref", probe + "/Ref/a.asmref", "{ \"reference\": \"MetaProbe\" }\n"),
                ("text", probe + "/a.json", "{ \"a\": 1 }\n"),
                ("uxml", probe + "/a.uxml", "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"></ui:UXML>\n"),
                ("uss", probe + "/a.uss", ".a { }\n"),
                ("tss", probe + "/a.tss", "@import url(\"unity-theme://default\");\n"),
                ("shader", probe + "/a.shader", "Shader \"MetaProbe/A\" { SubShader { Pass { } } }\n"),
                ("hlsl", probe + "/a.hlsl", "float4 MetaProbeA() { return 0; }\n"),
                ("cginc", probe + "/a.cginc", "float4 MetaProbeB() { return 0; }\n"),
                ("compute", probe + "/a.compute", "#pragma kernel CSMain\n[numthreads(1,1,1)] void CSMain(uint3 id : SV_DispatchThreadID) { }\n"),
            };
            foreach (var f in files)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(f.Path));
                File.WriteAllText(f.Path, f.Text);
            }
            File.WriteAllText(probe + "/Code/csc.rsp", "-langversion:14\n");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../tools/meta-templates"));
            Directory.CreateDirectory(outDir);
            void Capture(string key, string meta)
            {
                if (!File.Exists(meta)) throw new FileNotFoundException("no meta written for " + key, meta);
                var text = File.ReadAllText(meta).Replace("\r\n", "\n");
                text = Regex.Replace(text, "(?m)^guid: [0-9a-f]{32}$", "guid: {{GUID}}");
                text = Regex.Replace(text, "(?m)^timeCreated: .*\n", "");
                File.WriteAllText(Path.Combine(outDir, key + ".meta"), text);
            }
            Capture("folder", probe + "/Code.meta");
            foreach (var f in files) Capture(f.Key, f.Path + ".meta");
            var input = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.inputsystem");
            if (input != null) Capture("package", Path.Combine(input.resolvedPath, "package.json.meta"));
            else Debug.LogWarning("[lane] com.unity.inputsystem not resolved; the package template was not captured");

            AssetDatabase.DeleteAsset(probe);
            AssetDatabase.DeleteAsset(streaming);
            if (AssetDatabase.IsValidFolder("Assets/StreamingAssets") && AssetDatabase.FindAssets("", new[] { "Assets/StreamingAssets" }).Length == 0)
                AssetDatabase.DeleteAsset("Assets/StreamingAssets");
        });

        static void Run(string name, Action body)
        {
            try
            {
                body();
                Debug.Log("[lane] " + name + " ok");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[lane] error: " + name + ": " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
