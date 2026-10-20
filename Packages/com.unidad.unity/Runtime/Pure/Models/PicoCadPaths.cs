using System.Collections.Generic;

namespace Unidad.Adapter
{
    /// <summary>The picoCAD builder's asset paths (project-relative, '/' separated).</summary>
    internal static class PicoCadPaths
    {
        public const string MenuRoot = "Assets/Resources/Models";

        /// <summary>The manifest's texture file, else <c>&lt;name&gt;.png</c>, beside the glTF.</summary>
        public static string Texture(string gltfAssetPath, string textureFile, string modelName) =>
            Combine(Directory(gltfAssetPath), string.IsNullOrEmpty(textureFile) ? modelName + ".png" : textureFile);

        public static string Prefab(string targetFolder, string modelName) => Combine(targetFolder, modelName + ".prefab");

        /// <summary>The converter writes <c>&lt;name&gt;.manifest.json</c> beside <c>&lt;name&gt;.gltf</c>.</summary>
        public static string ManifestFor(string gltfAssetPath) =>
            gltfAssetPath.EndsWith(".gltf", System.StringComparison.OrdinalIgnoreCase)
                ? gltfAssetPath.Substring(0, gltfAssetPath.Length - ".gltf".Length) + ".manifest.json"
                : gltfAssetPath + ".manifest.json";

        public static string MenuTarget(string modelName) => Combine(MenuRoot, modelName);

        /// <summary>Every (parent, child) folder pair to create, root first: "Assets/A/B" → (Assets, A), (Assets/A, B).</summary>
        public static IReadOnlyList<(string Parent, string Child)> FolderSteps(string folder)
        {
            var parts = Normalize(folder).Split('/');
            var steps = new List<(string, string)>();
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                steps.Add((current, parts[i]));
                current = current + "/" + parts[i];
            }

            return steps;
        }

        public static string AnimationWarning(int tracks) =>
            $"WARN: {tracks} motion track(s) not imported (2.0 animates through Unidad Motion).";

        private static string Directory(string path)
        {
            var normalized = Normalize(path);
            var slash = normalized.LastIndexOf('/');
            return slash < 0 ? "" : normalized.Substring(0, slash);
        }

        private static string Combine(string folder, string file)
        {
            var left = Normalize(folder).TrimEnd('/');
            return left.Length == 0 ? file : left + "/" + file;
        }

        private static string Normalize(string path) => path.Replace('\\', '/');
    }
}
