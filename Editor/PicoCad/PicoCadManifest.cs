using System;
using System.IO;
using UnityEngine;

namespace Unidad.Core.Editor.PicoCad
{
    /// <summary>
    /// DTO for the converter's sidecar manifest (tools/picocad-pipeline emits
    /// <c>&lt;name&gt;.manifest.json</c> next to the glTF). JsonUtility-compatible subset —
    /// only the fields the Unity-side builder needs.
    /// </summary>
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
}
