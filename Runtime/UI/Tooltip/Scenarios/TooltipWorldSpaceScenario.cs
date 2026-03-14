using System.Collections.Generic;
using Unidad.Core.Testing;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using UnityEngine;
using IEventBus = Unidad.Core.EventBus.IEventBus;

namespace Unidad.Core.UI.Tooltip.Scenarios
{
    internal sealed class TooltipWorldSpaceScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter TooltipTextParam = new(
            "tooltipText", "Tooltip Text", typeof(string), "Cube");

        private static readonly ScenarioParameter CubeCountParam = new(
            "cubeCount", "Cube Count", typeof(int), 5, 1, 10);

        private static readonly ScenarioParameter SpacingParam = new(
            "spacing", "Spacing", typeof(float), 2f, 0.5f, 5f);

        private TooltipService _service;
        private int _cubesSpawned;

        public TooltipWorldSpaceScenario() : base(new TestScenarioDefinition(
            "tooltip-world-space",
            "Tooltip World-Space",
            "Hover over colored cubes to see world-space tooltips appear above them. " +
            "Tooltips billboard toward camera and disappear when the mouse leaves.",
            new[] { TooltipTextParam, CubeCountParam, SpacingParam }
        ))
        { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var tooltipText = ResolveParam<string>(overrides, "tooltipText");
            var cubeCount = ResolveParam<int>(overrides, "cubeCount");
            var spacing = ResolveParam<float>(overrides, "spacing");

            var eventBus = new Unidad.Core.EventBus.EventBus();
            var elementAnimator = new ElementAnimator();
            _service = new TooltipService(eventBus, elementAnimator);

            var root = SceneRoot;

            // Camera — reuse existing MainCamera or create one if none exists
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("[Scenario] Camera");
                camGo.transform.SetParent(root.transform);
                cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
            }

            var totalWidth = (cubeCount - 1) * spacing;
            cam.transform.position = new Vector3(totalWidth * 0.5f, 1.5f, -totalWidth * 0.5f - 3f);
            cam.transform.LookAt(new Vector3(totalWidth * 0.5f, 0.5f, 0));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.15f);

            // Color palette
            var colors = new[]
            {
                Color.red, Color.green, Color.blue, Color.yellow, Color.cyan,
                Color.magenta, new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 1f),
                new Color(0f, 1f, 0.5f), Color.white
            };

            _cubesSpawned = 0;

            for (var i = 0; i < cubeCount; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Cube_{i}";
                cube.transform.SetParent(root.transform);
                cube.transform.position = new Vector3(i * spacing, 0.5f, 0);

                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    renderer.sharedMaterial.color = colors[i % colors.Length];
                }

                var color = colors[i % colors.Length];
                var style = new TooltipStyle
                {
                    BackgroundColor = new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.9f),
                    BorderColor = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0.6f),
                    TextColor = Color.white
                };

                _service.Attach(cube, $"{tooltipText} #{i + 1}", style, new Vector3(0, 1f, 0));
                _cubesSpawned++;

                Debug.Log($"[TooltipScenario] Spawned cube {i + 1}/{cubeCount} at x={i * spacing}");
            }

            Debug.Log($"[TooltipScenario] World-space tooltip scenario ready: {cubeCount} cubes");
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
