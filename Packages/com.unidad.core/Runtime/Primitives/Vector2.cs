namespace Unidad.Core.Primitives
{
    /// <summary>
    /// Engine-neutral 2D float vector. Replaces UnityEngine.Vector2 in the pure-C# core
    /// (System.Numerics.Vector2 exists but uses uppercase X/Y and SIMD semantics we don't need).
    /// Field names match Unity's (lowercase x/y) — and mirror <see cref="Vector2Int"/> — so ported
    /// code and the Unity adapter read identically. Value equality via record struct.
    /// </summary>
    public readonly record struct Vector2(float x, float y)
    {
        public override string ToString() => $"({x}, {y})";
    }
}
