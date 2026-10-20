namespace Unidad.Core.Primitives
{
    /// <summary>
    /// Engine-neutral 2D integer vector. Replaces UnityEngine.Vector2Int in the pure-C# core
    /// (System.Numerics has no integer vector). Field names match Unity's (lowercase x/y) so
    /// ported code and the Unity adapter read identically. Value equality via record struct.
    /// </summary>
    public readonly record struct Vector2Int(int x, int y)
    {
        public override string ToString() => $"({x}, {y})";
    }
}
