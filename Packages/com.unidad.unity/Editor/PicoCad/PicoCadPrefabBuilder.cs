using System;
using System.Collections.Generic;

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

        public static PicoCadManifest Load(string path) => throw new NotImplementedException();
    }

    public sealed record PicoCadBuildReport(string PrefabPath, IReadOnlyList<string> Messages);

    /// <summary>Turns an imported picoCAD glTF into a prefab under a target folder (no Animator, no clips).</summary>
    public static class PicoCadPrefabBuilder
    {
        /// <param name="targetFolder">Such as "Assets/Resources/Models/Door".</param>
        public static PicoCadBuildReport Build(string gltfAssetPath, string manifestPath, string targetFolder) => throw new NotImplementedException();

        /// <summary>Arguments: <c>-unidadGltf &lt;Assets/...gltf&gt; -unidadManifest &lt;path&gt; -unidadOut &lt;Assets/Resources/...&gt;</c>.</summary>
        public static void BuildFromCommandLine() => throw new NotImplementedException();
    }
}
