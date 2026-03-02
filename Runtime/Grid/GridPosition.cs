using UnityEngine;

namespace Unidad.Core.Grid
{
    public readonly record struct GridPosition(int X, int Y)
    {
        public static implicit operator Vector2Int(GridPosition p) => new(p.X, p.Y);
        public static implicit operator GridPosition(Vector2Int v) => new(v.x, v.y);

        public Vector2 ToWorldCenter(float cellSize) =>
            new(X * cellSize + cellSize * 0.5f, Y * cellSize + cellSize * 0.5f);

        public override string ToString() => $"({X}, {Y})";
    }
}
