using System;
using System.Collections.Generic;
using Unidad.Core.Testing;
using Unidad.Core.UI.Components;
using Unidad.Core.UI.DesignSystem;
using Unidad.Core.UI.TextAnimation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Integration.TextAnimations
{
    internal sealed class TextAnimationSystemTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[] { typeof(ITextAnimationService) };

        public object CreateForTesting(TestDependencies deps) => new TextAnimationService();

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new TextAnimationServiceScenario();
        }
    }

    internal sealed class TextAnimationServiceScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter SampleTextParam = new(
            "sampleText", "Sample Text (supports animation tags)", typeof(string),
            "<wave>Wave text</wave> and <shake>shaking</shake> and <bounce>bouncing</bounce>!");

        private static readonly ScenarioParameter TypewriterSpeedParam = new(
            "typewriterSpeed", "Typewriter Speed (chars/sec)", typeof(float), 30f, 5f, 100f);

        private static readonly ScenarioParameter ShowFloatingParam = new(
            "showFloating", "Show Floating Text Demo", typeof(bool), true);

        private static readonly ScenarioParameter FloatingTextParam = new(
            "floatingText", "Floating Text Content", typeof(string), "-42 damage!");

        private TextAnimationService _service;

        public TextAnimationServiceScenario() : base(new TestScenarioDefinition(
            "text-animation-presets",
            "Text Animation Presets",
            "Shows animated labels using each preset — change the text and speed, then Re-run to see results live.",
            new[] { SampleTextParam, TypewriterSpeedParam, ShowFloatingParam, FloatingTextParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var sampleText = ResolveParam<string>(overrides, "sampleText");
            var typewriterSpeed = ResolveParam<float>(overrides, "typewriterSpeed");
            var showFloating = ResolveParam<bool>(overrides, "showFloating");
            var floatingText = ResolveParam<string>(overrides, "floatingText");

            _service = new TextAnimationService();

            // Override dialog preset speed from parameter
            var dialogPreset = TextAnimationPresets.CreateDialogPreset();
            dialogPreset.baseAppearanceSpeed = typewriterSpeed;
            _service.RegisterPreset("dialog", dialogPreset);

            var root = RootVisualElement;

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

            var title = new Label("Text Animation Presets");
            title.style.color = Color.white;
            title.style.fontSize = ThemeTokens.FontSizeXl;
            title.style.marginBottom = ThemeTokens.SpacingMd;
            container.Add(title);

            AddPresetDemo(container, "default", "Default (no typewriter)", sampleText);
            AddPresetDemo(container, "dialog", $"Dialog (typewriter {typewriterSpeed} chars/sec)", sampleText);
            AddPresetDemo(container, "title", "Title (typewriter 15 chars/sec)", sampleText);

            if (showFloating)
            {
                AddPresetDemo(container, "floating", "Floating (auto-vanish)",
                    _service.ApplyRecipe(FloatingTextStyle.Damage.RecipeName, floatingText));
            }
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var dialogPreset = _service.GetPreset("dialog");
            var typewriterSpeed = ResolveParam<float>(overrides, "typewriterSpeed");

            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("UIDocument created in scene", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Default preset exists", _service.GetPreset("default") != null, null),
                new("Dialog preset exists", dialogPreset != null, null),
                new("Floating preset exists", _service.GetPreset("floating") != null, null),
                new("Title preset exists", _service.GetPreset("title") != null, null),
                new("Dialog typewriter enabled",
                    dialogPreset is { enableTextAppearance: true },
                    dialogPreset is { enableTextAppearance: true } ? null : "Not enabled"),
                new($"Dialog speed is {typewriterSpeed}",
                    dialogPreset != null && Math.Abs(dialogPreset.baseAppearanceSpeed - typewriterSpeed) < 0.01f,
                    dialogPreset != null ? $"Speed was {dialogPreset.baseAppearanceSpeed}" : "Null")
            };
            return new ScenarioVerificationResult(checks);
        }

        private void AddPresetDemo(VisualElement parent, string presetName, string label, string text)
        {
            var row = new VisualElement();
            row.style.marginBottom = ThemeTokens.SpacingMd;
            row.style.backgroundColor = ThemeTokens.BgSurface;
            row.style.paddingTop = ThemeTokens.SpacingSm;
            row.style.paddingBottom = ThemeTokens.SpacingSm;
            row.style.paddingLeft = ThemeTokens.SpacingSm;
            row.style.paddingRight = ThemeTokens.SpacingSm;
            row.style.borderTopLeftRadius = ThemeTokens.RadiusMd;
            row.style.borderTopRightRadius = ThemeTokens.RadiusMd;
            row.style.borderBottomLeftRadius = ThemeTokens.RadiusMd;
            row.style.borderBottomRightRadius = ThemeTokens.RadiusMd;
            parent.Add(row);

            var header = new Label(label);
            header.style.color = ThemeTokens.TextSecondary;
            header.style.fontSize = ThemeTokens.FontSizeSm;
            header.style.marginBottom = ThemeTokens.SpacingXs;
            row.Add(header);

            var animatedLabel = _service.CreateLabel(presetName);
            animatedLabel.style.color = Color.white;
            animatedLabel.style.fontSize = ThemeTokens.FontSizeLg;
            row.Add(animatedLabel);

            _service.PlayTypewriter(animatedLabel, text);
        }

        protected override void OnCleanup() => _service?.Dispose();
    }
}
