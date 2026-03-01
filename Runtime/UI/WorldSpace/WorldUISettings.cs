using UnityEngine;

namespace Unidad.Core.UI.WorldSpace
{
    public sealed class WorldUISettings
    {
        public Vector3 Offset { get; set; } = Vector3.up;
        public bool Billboard { get; set; } = true;
        public float Scale { get; set; } = 0.01f;
        public int SortOrder { get; set; }

        public static WorldUISettings Default => new();
    }
}
