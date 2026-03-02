using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using UnityEngine;

namespace Unidad.Core.Grid
{
    internal sealed class Grid<TCell> : IGrid<TCell>
    {
        private readonly TCell[,] _cells;
        private readonly IEventBus _eventBus;

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        private static readonly (int dx, int dy)[] CardinalOffsets =
        {
            (0, 1), (1, 0), (0, -1), (-1, 0)
        };

        private static readonly (int dx, int dy)[] EightWayOffsets =
        {
            (0, 1), (1, 1), (1, 0), (1, -1),
            (0, -1), (-1, -1), (-1, 0), (-1, 1)
        };

        public Grid(int width, int height, float cellSize, IEventBus eventBus = null)
        {
            if (width <= 0) throw new ArgumentException("Width must be positive.", nameof(width));
            if (height <= 0) throw new ArgumentException("Height must be positive.", nameof(height));
            if (cellSize <= 0f) throw new ArgumentException("Cell size must be positive.", nameof(cellSize));

            Width = width;
            Height = height;
            CellSize = cellSize;
            _cells = new TCell[width, height];
            _eventBus = eventBus;
        }

        public TCell Get(GridPosition position)
        {
            ValidateBounds(position);
            return _cells[position.X, position.Y];
        }

        public void Set(GridPosition position, TCell value)
        {
            ValidateBounds(position);
            _cells[position.X, position.Y] = value;
            _eventBus?.Publish(new GridCellChangedEvent(position));
        }

        public bool IsInBounds(GridPosition position) =>
            position.X >= 0 && position.X < Width &&
            position.Y >= 0 && position.Y < Height;

        public GridPosition WorldToGrid(Vector2 worldPosition) =>
            new(Mathf.FloorToInt(worldPosition.x / CellSize),
                Mathf.FloorToInt(worldPosition.y / CellSize));

        public Vector2 GridToWorld(GridPosition position) =>
            new(position.X * CellSize + CellSize * 0.5f,
                position.Y * CellSize + CellSize * 0.5f);

        public IEnumerable<GridPosition> GetNeighbors(GridPosition pos, NeighborMode mode)
        {
            var offsets = mode == NeighborMode.Cardinal ? CardinalOffsets : EightWayOffsets;
            foreach (var (dx, dy) in offsets)
            {
                var neighbor = new GridPosition(pos.X + dx, pos.Y + dy);
                if (IsInBounds(neighbor))
                    yield return neighbor;
            }
        }

        public IEnumerable<GridPosition> AllPositions
        {
            get
            {
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                        yield return new GridPosition(x, y);
            }
        }

        public IEnumerable<(GridPosition Pos, TCell Cell)> Where(Func<TCell, bool> predicate)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (predicate(_cells[x, y]))
                        yield return (new GridPosition(x, y), _cells[x, y]);
        }

        private void ValidateBounds(GridPosition position)
        {
            if (!IsInBounds(position))
                throw new ArgumentOutOfRangeException(
                    nameof(position),
                    $"Position {position} is out of bounds ({Width}x{Height}).");
        }
    }
}
