using System.Collections.Generic;
using Unidad.Core.Testing;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using UnityEngine;
using IEventBus = Unidad.Core.EventBus.IEventBus;

namespace Unidad.Core.UI.Tooltip.Scenarios
{
    internal sealed class TooltipWorldHoverPersistScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter OffsetYParam = new(
            "offsetY", "Tooltip Offset Y", typeof(float), 0.8f, 0.3f, 2f);

        private TooltipService _service;
        private int _cubesSpawned;

        public TooltipWorldHoverPersistScenario() : base(new TestScenarioDefinition(
            "tooltip-world-hover-persist",
            "Tooltip World-Space — Hover Persist",
            "Hover a cube to show its tooltip, then move the mouse onto the tooltip panel itself. " +
            "The tooltip should stay visible as long as the mouse is over either the target cube or the tooltip. " +
            "Moving the mouse away from both should hide the tooltip.",
            new[] { OffsetYParam }
        ))
        { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var offsetY = ResolveParam<float>(overrides, "offsetY");

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

            cam.transform.position = new Vector3(1.5f, 1.5f, -4f);
            cam.transform.LookAt(new Vector3(1.5f, 0.5f, 0));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f);

            _cubesSpawned = 0;

            var labels = new[]
            {
                "Hover me, then move\nmouse onto this tooltip",
                "Small offset — easy\nto reach the tooltip",
                "Large cube — tooltip\nstays when hovering panel"
            };

            var scales = new[] { Vector3.one, Vector3.one * 0.6f, Vector3.one * 1.4f };

            for (var i = 0; i < 3; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"HoverCube_{i}";
                cube.transform.SetParent(root.transform);
                cube.transform.position = new Vector3(i * 1.5f, 0.5f, 0);
                cube.transform.localScale = scales[i];

                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    renderer.sharedMaterial.color = Color.HSVToRGB(0.55f + i * 0.15f, 0.7f, 0.9f);
                }

                var style = new TooltipStyle
                {
                    BackgroundColor = new Color(0.12f, 0.12f, 0.25f, 0.95f),
                    BorderColor = new Color(0.3f, 0.5f, 0.9f, 0.7f),
                    TextColor = Color.white
                };

                _service.Attach(cube, labels[i], style, new Vector3(0, offsetY, 0));
                _cubesSpawned++;

                Debug.Log($"[TooltipHoverPersistScenario] Spawned cube {i + 1}/3 — {labels[i].Replace("\n", " ")}");
            }

            Debug.Log("[TooltipHoverPersistScenario] Ready — hover a cube, then move mouse onto the tooltip");
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var driverExists = Object.FindAnyObjectByType<WorldTooltipDriver>() != null;

            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root exists", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Cubes spawned", _cubesSpawned == 3,
                    _cubesSpawned == 3 ? null : $"Expected 3, got {_cubesSpawned}"),
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
