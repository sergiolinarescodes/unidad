using System.Collections.Generic;
using Unidad.Core.Testing;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using UnityEngine;
using IEventBus = Unidad.Core.EventBus.IEventBus;

namespace Unidad.Core.UI.Tooltip.Scenarios
{
    internal sealed class TooltipWorldClampingScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter CubeCountParam = new(
            "cubeCount", "Cube Count", typeof(int), 7, 3, 12);

        private static readonly ScenarioParameter SpreadParam = new(
            "spread", "Spread", typeof(float), 4f, 2f, 8f);

        private TooltipService _service;
        private int _cubesSpawned;

        public TooltipWorldClampingScenario() : base(new TestScenarioDefinition(
            "tooltip-world-clamping",
            "Tooltip World-Space — Clamping",
            "Cubes placed near the edges and corners of the view. " +
            "Tooltips should stay fully on-screen with the reduced horizontal margin (1.28 half-width). " +
            "Move the camera or resize the window to verify edge clamping behaviour.",
            new[] { CubeCountParam, SpreadParam }
        ))
        { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var cubeCount = ResolveParam<int>(overrides, "cubeCount");
            var spread = ResolveParam<float>(overrides, "spread");

            var eventBus = new Unidad.Core.EventBus.EventBus();
            var elementAnimator = new ElementAnimator();
            _service = new TooltipService(eventBus, elementAnimator);

            var root = SceneRoot;

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("[Scenario] Camera");
                camGo.transform.SetParent(root.transform);
                cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
            }

            cam.transform.position = new Vector3(0, 2f, -6f);
            cam.transform.LookAt(Vector3.zero);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.12f);

            _cubesSpawned = 0;

            // Place cubes in an arc that pushes toward screen edges
            for (var i = 0; i < cubeCount; i++)
            {
                var angle = Mathf.Lerp(-70f, 70f, (float)i / Mathf.Max(1, cubeCount - 1));
                var rad = angle * Mathf.Deg2Rad;
                var x = Mathf.Sin(rad) * spread;
                var y = Mathf.Cos(rad) * 0.6f - 0.3f;

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"EdgeCube_{i}";
                cube.transform.SetParent(root.transform);
                cube.transform.localScale = Vector3.one * 0.6f;
                cube.transform.position = new Vector3(x, y, 0);

                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    renderer.material.color = Color.HSVToRGB((float)i / cubeCount, 0.8f, 1f);
                }

                var style = new TooltipStyle
                {
                    BackgroundColor = new Color(0.1f, 0.1f, 0.2f, 0.95f),
                    BorderColor = new Color(0.4f, 0.4f, 0.8f, 0.6f),
                    TextColor = Color.white
                };

                _service.Attach(cube, $"Edge cube #{i + 1} (angle {angle:F0}°)", style, new Vector3(0, 0.8f, 0));
                _cubesSpawned++;

                Debug.Log($"[TooltipClampingScenario] Spawned cube {i + 1}/{cubeCount} at angle={angle:F0}° x={x:F2}");
            }

            Debug.Log($"[TooltipClampingScenario] Ready — {cubeCount} cubes spread across view edges");
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var cubeCount = ResolveParam<int>(overrides, "cubeCount");
            var driverExists = Object.FindAnyObjectByType<WorldTooltipDriver>() != null;

            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root exists", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Cubes spawned", _cubesSpawned == cubeCount,
                    _cubesSpawned == cubeCount ? null : $"Expected {cubeCount}, got {_cubesSpawned}"),
                new("Tooltip service created", _service != null,
                    _service != null ? null : "Service is null"),
                new("WorldTooltipDriver active", driverExists,
                    driverExists ? null : "No WorldTooltipDriver found in scene")
            };

            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup()
        {
            _service?.Dispose();
            _service = null;
        }
    }
}
