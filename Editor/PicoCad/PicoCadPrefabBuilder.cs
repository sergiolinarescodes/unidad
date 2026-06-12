using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Unidad.Core.Editor.PicoCad
{
    /// <summary>
    /// Post-import step over a glTFast-imported picoCAD model:
    /// configures the texture importer (point filter, no compression, no mips),
    /// copies imported AnimationClips as looping non-legacy assets, generates an
    /// AnimatorController, and saves a game-facing prefab under Resources so
    /// <c>IGameObjectFactory.InstantiatePrefab</c> can load it.
    /// Invoked by the pipeline via Unity_RunCommand, or manually via the menu item.
    /// </summary>
    public static class PicoCadPrefabBuilder
    {
        public sealed class BuildReport
        {
            public string prefabPath;
            public string controllerPath;
            public readonly List<string> clipPaths = new();
            public readonly List<string> messages = new();

            public override string ToString() =>
                $"prefab={prefabPath} controller={controllerPath ?? "none"} clips=[{string.Join(", ", clipPaths)}]\n" +
                string.Join("\n", messages);
        }

        /// <param name="gltfAssetPath">Project-relative path of the imported .gltf (e.g. Assets/PicoCadImports/pig/pig.gltf).</param>
        /// <param name="manifestPath">Project-relative or absolute path of the converter's .manifest.json.</param>
        /// <param name="kindFolder">Folder under Assets/Resources/Models (e.g. "Critters"). Defaults to "Misc".</param>
        public static BuildReport Build(string gltfAssetPath, string manifestPath, string kindFolder = "Misc")
        {
            var report = new BuildReport();
            var manifest = PicoCadManifest.Load(manifestPath);

            var importedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(gltfAssetPath);
            if (importedRoot == null)
                throw new FileNotFoundException($"No imported GameObject at {gltfAssetPath} — is glTFast installed and the asset imported?");

            ConfigureTexture(gltfAssetPath, manifest, report);

            var targetFolder = $"Assets/Resources/Models/{kindFolder}";
            EnsureFolder(targetFolder);

            var clips = CopyAnimationClips(gltfAssetPath, manifest, targetFolder, report);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(importedRoot);
            instance.name = manifest.name;
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                if (clips.Count > 0)
                {
                    report.controllerPath = $"{targetFolder}/{manifest.name}_Controller.controller";
                    BuildController(report.controllerPath, clips, report);
                    var animator = instance.GetComponent<Animator>();
                    if (animator == null) animator = instance.AddComponent<Animator>();
                    animator.runtimeAnimatorController =
                        AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(report.controllerPath);
                }
                else
                {
                    // a model without motion tracks needs no Animator (PrimeTween effects only)
                    var animator = instance.GetComponent<Animator>();
                    if (animator != null) Object.DestroyImmediate(animator);
                }

                report.prefabPath = $"{targetFolder}/{manifest.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, report.prefabPath);
                report.messages.Add($"prefab saved: {report.prefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            return report;
        }

        static void ConfigureTexture(string gltfAssetPath, PicoCadManifest manifest, BuildReport report)
        {
            var textureName = manifest.files != null && !string.IsNullOrEmpty(manifest.files.texture)
                ? manifest.files.texture
                : manifest.name + ".png";
            var texturePath = Path.Combine(Path.GetDirectoryName(gltfAssetPath) ?? "", textureName).Replace('\\', '/');
            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
            {
                report.messages.Add($"WARN: texture not found at {texturePath} — filtering not configured");
                return;
            }

            var changed = importer.filterMode != FilterMode.Point ||
                          importer.textureCompression != TextureImporterCompression.Uncompressed ||
                          importer.mipmapEnabled ||
                          importer.alphaIsTransparency != manifest.usesTransparency;
            if (changed)
            {
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = manifest.usesTransparency;
                importer.SaveAndReimport();
                report.messages.Add($"texture configured: {texturePath} (point, uncompressed, no mips)");
            }
        }

        static List<AnimationClip> CopyAnimationClips(string gltfAssetPath, PicoCadManifest manifest,
            string targetFolder, BuildReport report)
        {
            var copies = new List<AnimationClip>();
            var imported = AssetDatabase.LoadAllAssetRepresentationsAtPath(gltfAssetPath)
                .OfType<AnimationClip>()
                .ToList();

            foreach (var clip in imported)
            {
                var copy = Object.Instantiate(clip);
                copy.name = clip.name;
                copy.legacy = false;
                var settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(copy, settings);

                var clipPath = $"{targetFolder}/{manifest.name}_{clip.name}.anim";
                AssetDatabase.CreateAsset(copy, clipPath);
                report.clipPaths.Add(clipPath);
                copies.Add(copy);
            }

            if (manifest.animations != null && manifest.animations.Length > 0 && copies.Count == 0)
                report.messages.Add("WARN: manifest declares animations but the glTF import yielded no AnimationClips — check glTFast animation import settings");

            return copies;
        }

        static void BuildController(string controllerPath, List<AnimationClip> clips, BuildReport report)
        {
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var clip in clips)
            {
                var state = stateMachine.AddState(clip.name);
                state.motion = clip;
            }
            report.messages.Add($"controller with {clips.Count} state(s): {controllerPath}");
        }

        static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        [MenuItem("Tools/PicoCAD/Build Prefab From Imported glTF…")]
        static void BuildFromSelectionMenu()
        {
            var gltfPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(gltfPath) || !gltfPath.EndsWith(".gltf"))
            {
                EditorUtility.DisplayDialog("PicoCAD", "Select an imported .gltf asset first.", "OK");
                return;
            }
            var manifestPath = gltfPath.Replace(".gltf", ".manifest.json");
            var report = Build(gltfPath, manifestPath);
            Debug.Log($"[PicoCadPrefabBuilder] {report}");
        }
    }
}
