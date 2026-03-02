using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.Grid
{
    public interface IGrid<TCell>
    {
        int Width { get; }
        int Height { get; }
        float CellSize { get; }
        TCell Get(GridPosition position);
        void Set(GridPosition position, TCell value);
        bool IsInBounds(GridPosition position);
        GridPosition WorldToGrid(Vector2 worldPosition);
        Vector2 GridToWorld(GridPosition position);
        IEnumerable<GridPosition> GetNeighbors(GridPosition pos, NeighborMode mode);
        IEnumerable<GridPosition> AllPositions { get; }
        IEnumerable<(GridPosition Pos, TCell Cell)> Where(Func<TCell, bool> predicate);
    }
}
