using System;
using System.Collections.Generic;
using TextAnimationsForUIToolkit;
using Unidad.Core.UI.TextAnimation;
using UnityEngine;

namespace Unidad.Core.Tests.Tests.UI
{
    public sealed class MockTextAnimationService : ITextAnimationService
    {
        private readonly Dictionary<string, TextAnimationSettings> _presets = new();
        private readonly List<string> _playedTexts = new();

        public TextAnimationSettings DefaultSettings { get; }
        public IReadOnlyList<string> PlayedTexts => _playedTexts;
        public int SkipCount { get; private set; }

        public MockTextAnimationService()
        {
            DefaultSettings = ScriptableObject.CreateInstance<TextAnimationSettings>();
            _presets["default"] = DefaultSettings;
        }

        public TextAnimationSettings GetPreset(string presetName)
        {
            return _presets.TryGetValue(presetName, out var s) ? s : DefaultSettings;
        }

        public void RegisterPreset(string presetName, TextAnimationSettings settings)
        {
            _presets[presetName] = settings;
        }

        public AnimatedLabel CreateLabel(string preset = "default")
        {
            var label = new AnimatedLabel();
            var settings = GetPreset(preset);
            if (settings != null)
                label.settings = settings;
            return label;
        }

        public AnimatedButton CreateButton(string preset = "default")
        {
            var button = new AnimatedButton();
            var settings = GetPreset(preset);
            if (settings != null)
                button.settings = settings;
            return button;
        }

        public void PlayTypewriter(AnimatedLabel label, string text, Action onComplete = null)
        {
            _playedTexts.Add(text);
            label.text = text;
            // In mock, immediately complete
            onComplete?.Invoke();
        }

        public void Skip(AnimatedLabel label)
        {
            SkipCount++;
            label.Skip();
        }
    }
}
