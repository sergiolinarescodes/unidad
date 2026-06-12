using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.AI.MCP.Editor.Helpers;
using Unity.AI.MCP.Editor.ToolRegistry;
using Unidad.Core.Editor.PicoCad;
using Unidad.Core.ModelCatalog;

namespace Unidad.Core.Editor.Mcp
{
    /// <summary>
    /// First-class MCP tools for the picoCAD → Unity pipeline. Registered
    /// automatically by Unity's McpToolRegistry at editor load (this assembly only
    /// compiles when com.unity.ai.assistant is installed — see the asmdef's
    /// defineConstraints). Agents call these instead of hand-rolled Unity.RunCommand
    /// snippets: one tool call per pipeline stage, structured results.
    /// </summary>
    public static class PicoCadMcpTools
    {
        const string ImportRoot = "Assets/PicoCadImports";
        const string PreviewPrefix = "PicoCadPreview__";

        // ---------------------------------------------------------------- Status

        public sealed class StatusParams
        {
            [McpDescription("Reserved; pass false or omit.")]
            public bool Verbose { get; set; }
        }

        [McpTool("Unidad.PicoCad.Status",
            "Health check for the Unidad picoCAD pipeline. Returns whether glTFast is installed, the registered model kinds and models, and the expected import/prefab folders. Call this first to probe the pipeline before importing.",
            "PicoCAD pipeline status",
            EnabledByDefault = true, Groups = new[] { "unidad", "picocad" })]
        public static object Status(StatusParams parameters)
        {
            try
            {
                var gltfast = Type.GetType("GLTFast.GltfImport, glTFast") != null;
                var kinds = ModelCatalogRegistry.LoadKinds();
                var models = ModelCatalogRegistry.LoadModels();
                return Response.Success("Unidad picoCAD pipeline status", new
                {
                    gltfastInstalled = gltfast,
                    importRoot = ImportRoot,
                    prefabRoot = "Assets/Resources/Models",
                    kindsRegistry = ModelCatalogRegistry.KindsPath,
                    modelsRegistry = ModelCatalogRegistry.ModelsPath,
                    kinds = kinds.Select(k => new { k.id, k.folder, k.unitScale, k.viewClass, k.effectProfile }).ToArray(),
                    models = models.Select(m => new { m.id, m.kindId, m.prefabPath }).ToArray(),
                });
            }
            catch (Exception ex)
            {
                return Response.Error(ex.Message);
            }
        }

        // ----------------------------------------------------------- ImportModel

        public sealed class ImportModelParams
        {
            [McpDescription("Directory containing the converter output (<name>.gltf/.bin/.png/.manifest.json). Absolute path or project-relative.", Required = true)]
            public string SourceDir { get; set; }

            [McpDescription("Catalog model id to register. Defaults to the manifest's model name.")]
            public string ModelId { get; set; }

            [McpDescription("Existing kind id from kinds.json (decides the prefab folder and conventions). Defaults to 'misc'.")]
            public string KindId { get; set; }

            [McpDescription("Register the model in models.json after the build. Default true.", Default = true)]
            public bool Register { get; set; } = true;
        }

        [McpTool("Unidad.PicoCad.ImportModel",
            "Full Unity import of a converted picoCAD model in ONE call: copies the glTF/texture/manifest into Assets/PicoCadImports/<name>/, imports synchronously via glTFast, runs PicoCadPrefabBuilder (point-filtered texture, looping AnimationClips, AnimatorController, prefab under Assets/Resources/Models/<KindFolder>/), and registers the model in the ModelCatalog. Returns prefab path, clip paths, and build messages.",
            "Import a picoCAD model",
            EnabledByDefault = true, Groups = new[] { "unidad", "picocad" })]
        public static object ImportModel(ImportModelParams parameters)
        {
            try
            {
                if (string.IsNullOrEmpty(parameters?.SourceDir))
                    return Response.Error("SourceDir is required");
                var sourceDir = Path.GetFullPath(parameters.SourceDir);
                if (!Directory.Exists(sourceDir))
                    return Response.Error($"SourceDir not found: {sourceDir}");

                var manifestFile = Directory.GetFiles(sourceDir, "*.manifest.json").FirstOrDefault();
                if (manifestFile == null)
                    return Response.Error($"No *.manifest.json in {sourceDir} — run the converter first");
                var manifest = PicoCadManifest.Load(manifestFile);
                var name = manifest.name;

                var destDir = $"{ImportRoot}/{name}";
                Directory.CreateDirectory(destDir);
                var copied = 0;
                foreach (var ext in new[] { ".gltf", ".bin", ".png", ".manifest.json" })
                {
                    var src = Path.Combine(sourceDir, name + ext);
                    if (!File.Exists(src)) continue;
                    File.Copy(src, Path.Combine(destDir, name + ext), overwrite: true);
                    copied++;
                }
                if (copied < 3)
                    return Response.Error($"Expected {name}.gltf/.bin/.png in {sourceDir}, copied only {copied} file(s)");

                AssetDatabase.ImportAsset(destDir,
                    ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);

                var kindId = string.IsNullOrEmpty(parameters.KindId) ? "misc" : parameters.KindId;
                var kind = ModelCatalogRegistry.FindKind(kindId);
                var folder = !string.IsNullOrEmpty(kind?.folder) ? kind.folder : "Misc";

                var report = PicoCadPrefabBuilder.Build(
                    $"{destDir}/{name}.gltf", $"{destDir}/{name}.manifest.json", folder);

                var modelId = string.IsNullOrEmpty(parameters.ModelId) ? name : parameters.ModelId;
                if (parameters.Register)
                {
                    ModelCatalogRegistry.UpsertModel(new ModelEntry
                    {
                        id = modelId,
                        kindId = kindId,
                        prefabPath = $"Models/{folder}/{name}",
                        clips = manifest.animations ?? Array.Empty<string>(),
                    });
                }

                return Response.Success($"Imported '{name}' as model '{modelId}' (kind {kindId})", new
                {
                    prefabPath = report.prefabPath,
                    controllerPath = report.controllerPath,
                    clipPaths = report.clipPaths.ToArray(),
                    messages = report.messages.ToArray(),
                    registered = parameters.Register,
                    modelId,
                    kindId,
                });
            }
            catch (Exception ex)
            {
                return Response.Error(ex.Message);
            }
        }

        // ---------------------------------------------------------- RegisterKind

        public sealed class RegisterKindParams
        {
            [McpDescription("Kind id (lowercase, stable).", Required = true)]
            public string Id { get; set; }

            [McpDescription("Human-readable name.")]
            public string DisplayName { get; set; }

            [McpDescription("Folder under Assets/Resources/Models (PascalCase).", Required = true)]
            public string Folder { get; set; }

            [McpDescription("Uniform scale applied at spawn.", Default = 1.0)]
            public float UnitScale { get; set; } = 1f;

            [McpDescription("Optional custom view class name (registered in the game's ModelCatalogInstaller).")]
            public string ViewClass { get; set; }

            [McpDescription("Effect profile id. Defaults to 'bounce'.")]
            public string EffectProfile { get; set; }

            [McpDescription("Comma-separated effect ids (e.g. 'spawn,despawn,hop').")]
            public string Effects { get; set; }

            [McpDescription("Free-form design notes enforced at verify time.")]
            public string DesignNotes { get; set; }
        }

        [McpTool("Unidad.PicoCad.RegisterKind",
            "Insert or update a model kind (archetype) in Assets/Resources/ModelCatalog/kinds.json: folder, unit scale, view class, effect profile, effects, design notes. Instances of a kind reuse all of this and skip design questions.",
            "Register a model kind",
            EnabledByDefault = true, Groups = new[] { "unidad", "picocad" })]
        public static object RegisterKind(RegisterKindParams parameters)
        {
            try
            {
                if (string.IsNullOrEmpty(parameters?.Id) || string.IsNullOrEmpty(parameters.Folder))
                    return Response.Error("Id and Folder are required");
                ModelCatalogRegistry.UpsertKind(new ModelKindDefinition
                {
                    id = parameters.Id,
                    displayName = string.IsNullOrEmpty(parameters.DisplayName) ? parameters.Id : parameters.DisplayName,
                    folder = parameters.Folder,
                    unitScale = parameters.UnitScale <= 0f ? 1f : parameters.UnitScale,
                    viewClass = parameters.ViewClass,
                    effectProfile = string.IsNullOrEmpty(parameters.EffectProfile) ? "bounce" : parameters.EffectProfile,
                    effects = string.IsNullOrEmpty(parameters.Effects)
                        ? Array.Empty<string>()
                        : parameters.Effects.Split(',').Select(e => e.Trim()).Where(e => e.Length > 0).ToArray(),
                    designNotes = parameters.DesignNotes,
                });
                return Response.Success($"Kind '{parameters.Id}' registered", new
                {
                    kinds = ModelCatalogRegistry.LoadKinds().Select(k => k.id).ToArray(),
                });
            }
            catch (Exception ex)
            {
                return Response.Error(ex.Message);
            }
        }

        // ---------------------------------------------------------- SpawnPreview

        public sealed class SpawnPreviewParams
        {
            [McpDescription("Catalog model id (resolved through models.json). Provide this or PrefabPath.")]
            public string ModelId { get; set; }

            [McpDescription("Prefab path override: 'Assets/...' asset path or Resources-relative (e.g. 'Models/Misc/pig').")]
            public string PrefabPath { get; set; }

            [McpDescription("Place a 10x10 ground quad under the model. Default true.", Default = true)]
            public bool AddGround { get; set; } = true;
        }

        [McpTool("Unidad.PicoCad.SpawnPreview",
            "Spawn an imported model prefab at the origin of the open scene for visual verification (optionally with a ground quad), frame it in the Scene view, and return its renderer bounds (height for scale checks) and animation clip names. Pair with Unity.SceneView captures and Unidad.PicoCad.CleanupPreview.",
            "Spawn a model preview",
            EnabledByDefault = true, Groups = new[] { "unidad", "picocad" })]
        public static object SpawnPreview(SpawnPreviewParams parameters)
        {
            try
            {
                string assetPath;
                var label = parameters?.ModelId;
                if (!string.IsNullOrEmpty(parameters?.PrefabPath))
                {
                    assetPath = parameters.PrefabPath.StartsWith("Assets/")
                        ? parameters.PrefabPath
                        : $"Assets/Resources/{parameters.PrefabPath}.prefab";
                    label ??= Path.GetFileNameWithoutExtension(assetPath);
                }
                else if (!string.IsNullOrEmpty(parameters?.ModelId))
                {
                    var entry = ModelCatalogRegistry.LoadModels().FirstOrDefault(m => m.id == parameters.ModelId);
                    if (entry == null)
                        return Response.Error($"Model '{parameters.ModelId}' not in {ModelCatalogRegistry.ModelsPath}");
                    assetPath = $"Assets/Resources/{entry.prefabPath}.prefab";
                }
                else
                {
                    return Response.Error("Provide ModelId or PrefabPath");
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                    return Response.Error($"No prefab at {assetPath}");

                var root = new GameObject($"{PreviewPrefix}{label}");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(root.transform, false);

                if (parameters?.AddGround != false)
                {
                    var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    ground.name = "Ground";
                    ground.transform.SetParent(root.transform, false);
                    ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    ground.transform.localScale = new Vector3(10f, 10f, 1f);
                }

                var renderers = instance.GetComponentsInChildren<Renderer>();
                var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
                foreach (var renderer in renderers.Skip(1))
                    bounds.Encapsulate(renderer.bounds);
                SceneView.lastActiveSceneView?.Frame(bounds, true);

                var animator = instance.GetComponentInChildren<Animator>();
                var clips = animator != null && animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.animationClips.Select(c => c.name).Distinct().ToArray()
                    : Array.Empty<string>();

                return Response.Success($"Preview '{root.name}' spawned from {assetPath}", new
                {
                    rootName = root.name,
                    prefabPath = assetPath,
                    height = bounds.size.y,
                    boundsCenter = new[] { bounds.center.x, bounds.center.y, bounds.center.z },
                    boundsSize = new[] { bounds.size.x, bounds.size.y, bounds.size.z },
                    clips,
                });
            }
            catch (Exception ex)
            {
                return Response.Error(ex.Message);
            }
        }

        // -------------------------------------------------------- CleanupPreview

        public sealed class CleanupPreviewParams
        {
            [McpDescription("Only remove the preview whose root matches this model id/label. Omit to remove all previews.")]
            public string Label { get; set; }
        }

        [McpTool("Unidad.PicoCad.CleanupPreview",
            "Destroy preview objects spawned by Unidad.PicoCad.SpawnPreview (scene roots named 'PicoCadPreview__*'). Returns how many were removed.",
            "Clean up model previews",
            EnabledByDefault = true, Groups = new[] { "unidad", "picocad" })]
        public static object CleanupPreview(CleanupPreviewParams parameters)
        {
            try
            {
                var targetName = string.IsNullOrEmpty(parameters?.Label) ? null : PreviewPrefix + parameters.Label;
                var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                    .Where(go => targetName != null ? go.name == targetName : go.name.StartsWith(PreviewPrefix))
                    .ToArray();
                foreach (var go in roots)
                    UnityEngine.Object.DestroyImmediate(go);
                return Response.Success($"Removed {roots.Length} preview(s)", new { removed = roots.Length });
            }
            catch (Exception ex)
            {
                return Response.Error(ex.Message);
            }
        }
    }
}
