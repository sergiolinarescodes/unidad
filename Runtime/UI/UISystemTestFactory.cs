using System;
using System.Collections.Generic;
using Unidad.Core.Testing;
using Unidad.Core.UI.Components;
using Unidad.Core.UI.DesignSystem;
using Unidad.Core.UI.Events;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using UnityEngine;
using UnityEngine.UIElements;
using IEventBus = Unidad.Core.EventBus.IEventBus;

namespace Unidad.Core.UI
{
    internal sealed class UISystemTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[]
        {
            typeof(IThemeService),
            typeof(IElementAnimator)
        };

        public object CreateForTesting(TestDependencies deps)
        {
            return new ThemeService(deps.EventBus);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new ThemeChangeScenario();
        }
    }

    internal sealed class ThemeChangeScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter ThemeNameParam = new(
            "themeName", "Theme Name", typeof(string), "dark");

        private static readonly ScenarioParameter ShowButtonsParam = new(
            "showButtons", "Show Button Variants", typeof(bool), true);

        private static readonly ScenarioParameter ShowProgressParam = new(
            "showProgress", "Show Progress Bar", typeof(bool), true);

        private static readonly ScenarioParameter ProgressValueParam = new(
            "progressValue", "Progress Value", typeof(float), 0.7f, 0f, 1f);

        private ThemeService _themeService;
        private IEventBus _eventBus;
        private bool _themeChangedFired;
        private string _receivedThemeName;

        public ThemeChangeScenario() : base(new TestScenarioDefinition(
            "theme-change",
            "Theme Change",
            "Renders a themed panel with color swatches, buttons, and progress bar using design tokens. Change parameters and Re-run to see updates.",
            new[] { ThemeNameParam, ShowButtonsParam, ShowProgressParam, ProgressValueParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var themeName = ResolveParam<string>(overrides, "themeName");
            var showButtons = ResolveParam<bool>(overrides, "showButtons");
            var showProgress = ResolveParam<bool>(overrides, "showProgress");
            var progressValue = ResolveParam<float>(overrides, "progressValue");

            _themeChangedFired = false;
            _receivedThemeName = null;

            _eventBus = new Unidad.Core.EventBus.EventBus();
            _themeService = new ThemeService(_eventBus);

            _eventBus.Subscribe<ThemeChangedEvent>(evt =>
            {
                _themeChangedFired = true;
                _receivedThemeName = evt.ThemeName;
            });

            var root = RootVisualElement;
            _themeService.ApplyTo(root);

            var container = new VisualElement();
            container.style.backgroundColor = ThemeTokens.BgPrimary;
            container.style.paddingTop = ThemeTokens.SpacingLg;
            container.style.paddingBottom = ThemeTokens.SpacingLg;
            container.style.paddingLeft = ThemeTokens.SpacingLg;
            container.style.paddingRight = ThemeTokens.SpacingLg;
            container.style.position = Position.Absolute;
            container.style.left = 0;
            container.style.right = 0;
            container.style.top = 0;
            container.style.bottom = 0;
            root.Add(container);

            var title = new UnidadLabel($"Theme: {themeName}");
            title.SetSize(LabelSize.ExtraLarge);
            container.Add(title);

            var subtitle = new UnidadLabel("Design tokens applied via ThemeTokens + USS");
            subtitle.SetTextStyle(LabelStyle.Secondary);
            container.Add(subtitle);

            // Color swatches
            var swatchRow = new VisualElement();
            swatchRow.style.flexDirection = FlexDirection.Row;
            swatchRow.style.marginTop = ThemeTokens.SpacingMd;
            container.Add(swatchRow);

            AddSwatch(swatchRow, "Primary", ThemeTokens.Primary);
            AddSwatch(swatchRow, "Secondary", ThemeTokens.Secondary);
            AddSwatch(swatchRow, "Success", ThemeTokens.Success);
            AddSwatch(swatchRow, "Warning", ThemeTokens.Warning);
            AddSwatch(swatchRow, "Error", ThemeTokens.Error);

            if (showButtons)
            {
                var buttonRow = new VisualElement();
                buttonRow.style.flexDirection = FlexDirection.Row;
                buttonRow.style.marginTop = ThemeTokens.SpacingMd;
                container.Add(buttonRow);

                AddButton(buttonRow, "Primary", ButtonVariant.Primary);
                AddButton(buttonRow, "Secondary", ButtonVariant.Secondary);
                AddButton(buttonRow, "Success", ButtonVariant.Success);
                AddButton(buttonRow, "Warning", ButtonVariant.Warning);
                AddButton(buttonRow, "Danger", ButtonVariant.Danger);

                var disabledBtn = new UnidadButton("Disabled");
                disabledBtn.IsDisabled = true;
                disabledBtn.style.marginLeft = ThemeTokens.SpacingSm;
                buttonRow.Add(disabledBtn);
            }

            if (showProgress)
            {
                var progress = new UnidadProgressBar(progressValue);
                progress.style.marginTop = ThemeTokens.SpacingMd;
                progress.style.width = 400;
                container.Add(progress);

                var progressLabel = new UnidadLabel($"Progress: {progressValue:P0}");
                progressLabel.SetTextStyle(LabelStyle.Muted);
                progressLabel.style.marginTop = ThemeTokens.SpacingXs;
                container.Add(progressLabel);
            }

            _themeService.SetTheme(themeName, null);
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var themeName = ResolveParam<string>(overrides, "themeName");
            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("UIDocument created in scene", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("ThemeChangedEvent fired", _themeChangedFired,
                    _themeChangedFired ? null : "Event was not published"),
                new($"Theme name is '{themeName}'", _receivedThemeName == themeName,
                    _receivedThemeName == themeName ? null : $"Expected '{themeName}', got '{_receivedThemeName}'"),
                new("CurrentTheme updated", _themeService.CurrentTheme == themeName,
                    _themeService.CurrentTheme == themeName ? null : $"Got '{_themeService.CurrentTheme}'")
            };
            return new ScenarioVerificationResult(checks);
        }

        private static void AddSwatch(VisualElement parent, string label, Color color)
        {
            var swatch = new VisualElement();
            swatch.style.width = 60;
            swatch.style.height = 60;
            swatch.style.backgroundColor = color;
            swatch.style.borderTopLeftRadius = ThemeTokens.RadiusMd;
            swatch.style.borderTopRightRadius = ThemeTokens.RadiusMd;
            swatch.style.borderBottomLeftRadius = ThemeTokens.RadiusMd;
            swatch.style.borderBottomRightRadius = ThemeTokens.RadiusMd;
            swatch.style.marginRight = ThemeTokens.SpacingSm;
            swatch.style.alignItems = Align.Center;
            swatch.style.justifyContent = Justify.Center;

            var lbl = new Label(label);
            lbl.style.color = Color.white;
            lbl.style.fontSize = ThemeTokens.FontSizeXs;
            swatch.Add(lbl);

            parent.Add(swatch);
        }

        private static void AddButton(VisualElement parent, string label, ButtonVariant variant)
        {
            var btn = new UnidadButton(label);
            btn.SetVariant(variant);
            if (parent.childCount > 0)
                btn.style.marginLeft = ThemeTokens.SpacingSm;
            parent.Add(btn);
        }
    }
}
