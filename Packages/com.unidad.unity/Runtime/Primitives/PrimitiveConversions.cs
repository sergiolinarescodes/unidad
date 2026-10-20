namespace Unidad.Adapter
{
    /// <summary>Core and System.Numerics values to Unity's and back.</summary>
    public static class PrimitiveConversions
    {
        public static UnityEngine.Vector2 ToUnity(this Unidad.Core.Primitives.Vector2 v) => new(v.x, v.y);

        public static Unidad.Core.Primitives.Vector2 ToCore(this UnityEngine.Vector2 v) => new(v.x, v.y);

        public static UnityEngine.Vector2Int ToUnity(this Unidad.Core.Primitives.Vector2Int v) => new(v.x, v.y);

        public static Unidad.Core.Primitives.Vector2Int ToCore(this UnityEngine.Vector2Int v) => new(v.x, v.y);

        public static UnityEngine.Vector3 ToUnity(this System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

        public static System.Numerics.Vector3 ToNumerics(this UnityEngine.Vector3 v) => new(v.x, v.y, v.z);
    }
}
