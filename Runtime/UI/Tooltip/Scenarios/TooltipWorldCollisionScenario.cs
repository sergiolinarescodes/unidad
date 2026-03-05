using System.Collections.Generic;
using Unidad.Core.Testing;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using UnityEngine;
using IEventBus = Unidad.Core.EventBus.IEventBus;

namespace Unidad.Core.UI.Tooltip.Scenarios
{
    internal sealed class TooltipWorldCollisionScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter CollisionModeParam = new(
            "collisionMode", "Collision Mode", typeof(int), 1, 0, 2);

        private static readonly ScenarioParameter OffsetYParam = new(
            "offsetY", "Tooltip Offset Y", typeof(float), 0.6f, 0.1f, 2f);

        private static readonly ScenarioParameter ObstacleCountParam = new(
            "obstacleCount", "Obstacle Count", typeof(int), 3, 0, 6);

        private static readonly ScenarioParameter ShowModeParam = new(
            "showMode", "Show Mode", typeof(int), 1, 0, 1);

        private TooltipService _service;
        private int _objectsSpawned;

        public TooltipWorldCollisionScenario() : base(new TestScenarioDefinition(
            "tooltip-world-collision",
            "Tooltip World-Space — Collision",
            "Tests tooltip collision avoidance. Cubes have tooltips with a low Y offset so they overlap the target. " +
            "Collision Mode: 0=None (clips through), 1=TargetOnly (push from target), 2=AllObjects (push from everything). " +
            "Show Mode: 0=Instant (appear immediately), 1=FadeIn (fade in over 0.15s). " +
            "Obstacles are placed near targets to test AllObjects mode.",
            new[] { CollisionModeParam, OffsetYParam, ObstacleCountParam, ShowModeParam }
        ))
        { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var collisionModeInt = ResolveParam<int>(overrides, "collisionMode");
            var offsetY = ResolveParam<float>(overrides, "offsetY");
            var obstacleCount = ResolveParam<int>(overrides, "obstacleCount");
            var showModeInt = ResolveParam<int>(overrides, "showMode");

            var collision = collisionModeInt switch
            {
                1 => WorldTooltipCollision.TargetOnly,
                2 => WorldTooltipCollision.AllObjects,
                _ => WorldTooltipCollision.None
            };

            var showMode = showModeInt == 0
                ? WorldTooltipShowMode.Instant
                : WorldTooltipShowMode.FadeIn;

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

            cam.transform.position = new Vector3(2f, 3f, -5f);
            cam.transform.LookAt(new Vector3(2f, 0.5f, 0));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.1f);

            _objectsSpawned = 0;

            // Spawn 3 target cubes with tooltips
            for (var i = 0; i < 3; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"TargetCube_{i}";
                cube.transform.SetParent(root.transform);
                cube.transform.position = new Vector3(i * 2f, 0.5f, 0);

                ScenarioHelpers.SetColor(cube, Color.HSVToRGB(i * 0.33f, 0.7f, 0.9f));

                var style = new TooltipStyle
                {
                    BackgroundColor = new Color(0.15f, 0.1f, 0.2f, 0.95f),
                    BorderColor = new Color(0.6f, 0.3f, 0.8f, 0.7f),
                    TextColor = Color.white
                };

                _service.Attach(cube, $"Collision: {collision}\nOffset Y: {offsetY:F1}", style,
                    new Vector3(0, offsetY, 0), collision, showMode);
                _objectsSpawned++;

                Debug.Log($"[TooltipCollisionScenario] Target cube {i + 1}/3 at x={i * 2f}, collision={collision}");
            }

            // Spawn obstacles above targets (to test AllObjects push)
            for (var i = 0; i < obstacleCount; i++)
            {
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obstacle.name = $"Obstacle_{i}";
                obstacle.transform.SetParent(root.transform);
                obstacle.transform.localScale = Vector3.one * 0.5f;
                obstacle.transform.position = new Vector3(
                    (i % 3) * 2f,
                    1.0f + offsetY,
                    0.2f * (i / 3));

                ScenarioHelpers.SetColor(obstacle, new Color(1f, 0.3f, 0.3f, 0.5f));

                _objectsSpawned++;

                Debug.Log($"[TooltipCollisionScenario] Obstacle {i + 1}/{obstacleCount} at ({obstacle.transform.position})");
            }

            Debug.Log($"[TooltipCollisionScenario] Ready — collision={collision}, offsetY={offsetY:F1}, obstacles={obstacleCount}, showMode={showMode}");
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var obstacleCount = ResolveParam<int>(overrides, "obstacleCount");
            var expectedTotal = 3 + obstacleCount;
            var driverExists = Object.FindAnyObjectByType<WorldTooltipDriver>() != null;

            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root exists", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Objects spawned", _objectsSpawned == expectedTotal,
                    _objectsSpawned == expectedTotal ? null : $"Expected {expectedTotal}, got {_objectsSpawned}"),
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
