using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;
using UnityEngine;

namespace Unidad.Core.Grid.Scenarios
{
    /// <summary>
    /// Visual scenario: spawns a grid of colored quads.
    /// Cells toggle between two colors when clicked. Grid events are logged to the Console.
    /// </summary>
    internal sealed class GridScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter WidthParam = new(
            "width", "Grid Width", typeof(int), 5, 2, 10);

        private static readonly ScenarioParameter HeightParam = new(
            "height", "Grid Height", typeof(int), 5, 2, 10);

        private static readonly ScenarioParameter CellSizeParam = new(
            "cellSize", "Cell Size", typeof(float), 1f, 0.5f, 3f);

        private IEventBus _eventBus;
        private IGrid<int> _grid;
        private readonly List<IDisposable> _subscriptions = new();
        private readonly List<(GameObject Go, Material Mat)> _cellObjects = new();
        private int _gridWidth;
        private int _gridHeight;

        private static readonly Color ColorOff = new(0.3f, 0.3f, 0.4f);
        private static readonly Color ColorOn = new(0.2f, 0.8f, 0.3f);

        public GridScenario() : base(new TestScenarioDefinition(
            "grid-visual",
            "Grid Visual (Live)",
            "Spawns a grid of colored quads. Cell values are toggled by clicking. " +
            "Grid change events are logged to the Console.",
            new[] { WidthParam, HeightParam, CellSizeParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            _gridWidth = Mathf.Clamp(ResolveParam<int>(overrides, "width"), 2, 10);
            _gridHeight = Mathf.Clamp(ResolveParam<int>(overrides, "height"), 2, 10);
            var cellSize = ResolveParam<float>(overrides, "cellSize");

            _cellObjects.Clear();

            // --- Services ---
            _eventBus = new EventBus.EventBus();
            var factory = new GridFactory(_eventBus);
            _grid = factory.Create<int>(_gridWidth, _gridHeight, cellSize);

            // --- Subscribe to cell changes ---
            _subscriptions.Add(_eventBus.Subscribe<GridCellChangedEvent>(evt =>
            {
                var value = _grid.Get(evt.Position);
                Debug.Log($"[GridScenario] Cell changed: {evt.Position} -> {value}");
                UpdateCellColor(evt.Position, value);
            }));

            // --- Spawn quads ---
            var offset = new Vector3(
                -(_gridWidth * cellSize) * 0.5f + cellSize * 0.5f,
                -(_gridHeight * cellSize) * 0.5f + cellSize * 0.5f,
                0f);

            for (int y = 0; y < _gridHeight; y++)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = $"[Scenario] Cell ({x},{y})";
                    quad.transform.SetParent(SceneRoot.transform);
                    quad.transform.localPosition = offset + new Vector3(x * cellSize, y * cellSize, 0f);
                    quad.transform.localScale = new Vector3(cellSize * 0.9f, cellSize * 0.9f, 1f);

                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = ColorOff;
                    quad.GetComponent<Renderer>().sharedMaterial = mat;

                    _cellObjects.Add((quad, mat));
                }
            }

            // --- Toggle some cells to show visual variety ---
            for (int i = 0; i < _gridWidth; i++)
            {
                _grid.Set(new GridPosition(i, i % _gridHeight), 1);
            }

            Debug.Log($"[GridScenario] Started — {_gridWidth}x{_gridHeight} grid, cellSize={cellSize}");
        }

        private void UpdateCellColor(GridPosition pos, int value)
        {
            var index = pos.Y * _gridWidth + pos.X;
            if (index < 0 || index >= _cellObjects.Count) return;
            var (_, mat) = _cellObjects[index];
            if (mat != null)
                mat.color = value == 0 ? ColorOff : ColorOn;
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var expectedCount = _gridWidth * _gridHeight;
            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root created", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Grid created", _grid != null,
                    _grid != null ? null : "Grid is null"),
                new($"Grid dimensions are {_gridWidth}x{_gridHeight}",
                    _grid != null && _grid.Width == _gridWidth && _grid.Height == _gridHeight,
                    _grid != null && _grid.Width == _gridWidth && _grid.Height == _gridHeight
                        ? null : "Grid dimensions mismatch"),
                new($"All {expectedCount} cell quads spawned",
                    _cellObjects.Count == expectedCount,
                    _cellObjects.Count == expectedCount
                        ? null : $"Expected {expectedCount}, got {_cellObjects.Count}")
            };
            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup()
        {
            foreach (var sub in _subscriptions) sub.Dispose();
            _subscriptions.Clear();

            foreach (var (_, mat) in _cellObjects)
            {
                if (mat != null)
                    UnityEngine.Object.DestroyImmediate(mat);
            }
            _cellObjects.Clear();

            _eventBus?.ClearAllSubscriptions();
            _eventBus = null;
            _grid = null;
        }
    }
}
