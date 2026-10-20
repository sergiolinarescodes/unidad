using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UObject = UnityEngine.Object;

namespace Unidad.Adapter.Editor.PicoCad
{
    /// <summary>The picoCAD pipeline's manifest beside an exported glTF (0.1.0's fields, verbatim).</summary>
    [Serializable]
    public sealed class PicoCadManifest
    {
        public int schema;
        public string name;
        public string sourceFile;
        public string picoCadVersion;
        public float scale;
        public string[] palette;
        public int transparentColor;
        public bool usesTransparency;
        public int meshCount;
        public int faceCount;
        public float motionDuration;
        public string[] animations;
        public ManifestFiles files;
        public string[] warnings;

        [Serializable]
        public sealed class ManifestFiles
        {
            public string gltf;
            public string texture;
            public string obj;
        }

        public static PicoCadManifest Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"PicoCad manifest not found: {path}");
            var manifest = JsonUtility.FromJson<PicoCadManifest>(File.ReadAllText(path));
            if (manifest == null || string.IsNullOrEmpty(manifest.name))
                throw new InvalidDataException($"PicoCad manifest invalid: {path}");
            return manifest;
        }
    }

    public sealed record PicoCadBuildReport(string PrefabPath, IReadOnlyList<string> Messages)
    {
        public override string ToString() => $"prefab={PrefabPath}\n{string.Join("\n", Messages)}";
    }

    /// <summary>Turns an imported picoCAD glTF into a prefab under a target folder (no Animator, no clips).</summary>
    public static class PicoCadPrefabBuilder
    {
        /// <param name="targetFolder">Such as "Assets/Resources/Models/Door".</param>
        public static PicoCadBuildReport Build(string gltfAssetPath, string manifestPath, string targetFolder)
        {
            var messages = new List<string>();
            var manifest = PicoCadManifest.Load(manifestPath);

            var importedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(gltfAssetPath);
            if (importedRoot == null)
                throw new FileNotFoundException(
                    $"No imported GameObject at {gltfAssetPath}; is com.unity.cloud.gltfast in the project manifest and the asset imported?");

            ConfigureTexture(gltfAssetPath, manifest, messages);
            EditorFolders.Ensure(targetFolder);
            if (manifest.animations is { Length: > 0 })
                messages.Add(PicoCadPaths.AnimationWarning(manifest.animations.Length));

            var prefabPath = PicoCadPaths.Prefab(targetFolder, manifest.name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(importedRoot);
            instance.name = manifest.name;
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                    UObject.DestroyImmediate(animator);
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                messages.Add($"prefab saved: {prefabPath}");
            }
            finally
            {
                UObject.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            return new PicoCadBuildReport(prefabPath, messages);
        }

        /// <summary>Arguments: <c>-unidadGltf &lt;Assets/...gltf&gt; -unidadManifest &lt;path&gt; -unidadOut &lt;Assets/Resources/...&gt;</c>.</summary>
        public static void BuildFromCommandLine() => BatchExit.Run("PicoCadPrefabBuilder.BuildFromCommandLine", () =>
        {
            var args = CliArgs.FromProcess();
            var report = Build(args.Required("-unidadGltf"), args.Required("-unidadManifest"), args.Required("-unidadOut"));
            Debug.Log($"[PicoCadPrefabBuilder] {report}");
            return null;
        });

        [MenuItem("Tools/Unidad/PicoCAD/Build Prefab From Imported glTF…")]
        private static void BuildFromSelectionMenu()
        {
            var gltfPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(gltfPath) || !gltfPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("PicoCAD", "Select an imported .gltf asset first.", "OK");
                return;
            }

            var name = Path.GetFileNameWithoutExtension(gltfPath);
            var report = Build(gltfPath, PicoCadPaths.ManifestFor(gltfPath), PicoCadPaths.MenuTarget(name));
            Debug.Log($"[PicoCadPrefabBuilder] {report}");
        }

        private static void ConfigureTexture(string gltfAssetPath, PicoCadManifest manifest, List<string> messages)
        {
            var texturePath = PicoCadPaths.Texture(gltfAssetPath, manifest.files?.texture, manifest.name);
            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
            {
                messages.Add($"WARN: texture not found at {texturePath} — filtering not configured");
                return;
            }

            var changed = importer.filterMode != FilterMode.Point ||
                          importer.textureCompression != TextureImporterCompression.Uncompressed ||
                          importer.mipmapEnabled ||
                          importer.alphaIsTransparency != manifest.usesTransparency;
            if (!changed) return;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = manifest.usesTransparency;
            importer.SaveAndReimport();
            messages.Add($"texture configured: {texturePath} (point, uncompressed, no mips)");
        }
    }
}
