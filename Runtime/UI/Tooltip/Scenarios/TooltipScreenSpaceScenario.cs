using System;
using System.Collections.Generic;
using Unidad.Core.Testing;
using Unidad.Core.UI.Components;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using UnityEngine.UIElements;
using IEventBus = Unidad.Core.EventBus.IEventBus;

namespace Unidad.Core.UI.Tooltip.Scenarios
{
    internal sealed class TooltipScreenSpaceScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter TooltipTextParam = new(
            "tooltipText", "Tooltip Text", typeof(string), "This is a tooltip!");

        private static readonly ScenarioParameter PlacementParam = new(
            "placement", "Placement", typeof(int), 4, 0, 4);

        private static readonly ScenarioParameter ShowArrowParam = new(
            "showArrow", "Show Arrow", typeof(bool), true);

        private static readonly ScenarioParameter ButtonCountParam = new(
            "buttonCount", "Button Count", typeof(int), 9, 1, 12);

        private TooltipService _service;
        private readonly List<IDisposable> _hoverSubscriptions = new();
        private int _buttonsCreated;

        public TooltipScreenSpaceScenario() : base(new TestScenarioDefinition(
            "tooltip-screen-space",
            "Tooltip Screen-Space",
            "Buttons placed at corners, edges, and center with hover tooltips. " +
            "Hover to see border detection. Click a button to show a persistent tooltip. " +
            "Placement: 0=Top, 1=Bottom, 2=Left, 3=Right, 4=Auto.",
            new[] { TooltipTextParam, PlacementParam, ShowArrowParam, ButtonCountParam }
        ))
        { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var tooltipText = ResolveParam<string>(overrides, "tooltipText");
            var placementInt = ResolveParam<int>(overrides, "placement");
            var showArrow = ResolveParam<bool>(overrides, "showArrow");
            var buttonCount = ResolveParam<int>(overrides, "buttonCount");

            var placement = (TooltipPlacement)placementInt;
            var style = new TooltipStyle { ShowArrow = showArrow };

            var eventBus = new Unidad.Core.EventBus.EventBus();
            var elementAnimator = new ElementAnimator();
            _service = new TooltipService(eventBus, elementAnimator);

            var root = RootVisualElement;

            // Full-screen background
            root.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.08f, 0.08f, 0.15f));

            // Tooltip layer (absolute overlay)
            var tooltipLayer = new VisualElement
            {
                name = "tooltip-layer",
                pickingMode = PickingMode.Ignore
            };
            tooltipLayer.style.position = Position.Absolute;
            tooltipLayer.style.left = 0;
            tooltipLayer.style.right = 0;
            tooltipLayer.style.top = 0;
            tooltipLayer.style.bottom = 0;
            root.Add(tooltipLayer);

            _service.SetTooltipLayer(tooltipLayer);

            // Button container — grid layout filling the screen
            var grid = new VisualElement { name = "button-grid" };
            grid.style.position = Position.Absolute;
            grid.style.left = 20;
            grid.style.right = 20;
            grid.style.top = 20;
            grid.style.bottom = 20;
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.SpaceBetween;
            grid.style.alignContent = Align.Stretch;
            root.Insert(0, grid);

            // Positions: corners, edges, center
            var positions = new[]
            {
                "Top-Left", "Top-Center", "Top-Right",
                "Left", "Center", "Right",
                "Bottom-Left", "Bottom-Center", "Bottom-Right",
                "Extra-1", "Extra-2", "Extra-3"
            };

            _buttonsCreated = 0;
            TooltipHandle clickTooltip = null;

            for (var i = 0; i < buttonCount && i < positions.Length; i++)
            {
                var label = positions[i];
                var btn = new UnidadButton(label);
                btn.style.width = Length.Percent(30);
                btn.style.height = 60;
                btn.style.marginBottom = 10;

                var content = TooltipContent.FromText($"{tooltipText}\n({label})");

                // Hover tooltip
                var sub = _service.RegisterHover(btn, content, placement, style);
                _hoverSubscriptions.Add(sub);

                // Click: show persistent tooltip
                var capturedBtn = btn;
                btn.Clicked += () =>
                {
                    if (clickTooltip != null)
                        _service.Hide(clickTooltip);

                    var anchor = TooltipAnchor.FromElement(capturedBtn);
                    clickTooltip = _service.Show(
                        TooltipContent.FromText($"Clicked: {label}"),
                        anchor, placement, style);
                };

                grid.Add(btn);
                _buttonsCreated++;
            }
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var buttonCount = ResolveParam<int>(overrides, "buttonCount");
            var expected = Math.Min(buttonCount, 12);

            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root exists", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Tooltip service created", _service != null,
                    _service != null ? null : "Service is null"),
                new("Buttons spawned", _buttonsCreated == expected,
                    _buttonsCreated == expected ? null : $"Expected {expected}, got {_buttonsCreated}")
            };

            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup()
        {
            foreach (var sub in _hoverSubscriptions)
                sub.Dispose();
            _hoverSubscriptions.Clear();

            _service?.Dispose();
            _service = null;
        }
    }
}
