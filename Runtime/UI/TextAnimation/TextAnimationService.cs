using System;
using System.Collections.Generic;
using TextAnimationsForUIToolkit;
using TextAnimationsForUIToolkit.Events;

namespace Unidad.Core.UI.TextAnimation
{
    internal sealed class TextAnimationService : ITextAnimationService, IDisposable
    {
        private readonly Dictionary<string, TextAnimationSettings> _presets = new();

        public TextAnimationSettings DefaultSettings { get; }

        public TextAnimationService(TextAnimationSettings defaultSettings = null)
        {
            DefaultSettings = defaultSettings ?? TextAnimationPresets.CreateDefaultPreset();
            _presets["default"] = DefaultSettings;
            _presets["dialog"] = TextAnimationPresets.CreateDialogPreset();
            _presets["floating"] = TextAnimationPresets.CreateFloatingPreset();
            _presets["title"] = TextAnimationPresets.CreateTitlePreset();
        }

        public TextAnimationSettings GetPreset(string presetName)
        {
            return _presets.TryGetValue(presetName, out var settings) ? settings : DefaultSettings;
        }

        public void RegisterPreset(string presetName, TextAnimationSettings settings)
        {
            _presets[presetName] = settings;
        }

        public AnimatedLabel CreateLabel(string preset = "default")
        {
            var label = new AnimatedLabel
            {
                settings = GetPreset(preset)
            };
            return label;
        }

        public AnimatedButton CreateButton(string preset = "default")
        {
            var button = new AnimatedButton
            {
                settings = GetPreset(preset)
            };
            return button;
        }

        public void PlayTypewriter(AnimatedLabel label, string text, Action onComplete = null)
        {
            if (onComplete != null)
            {
                void OnFinished(TextAppearanceFinishedEvent _)
                {
                    label.textAppearanceFinished -= OnFinished;
                    onComplete();
                }
                label.textAppearanceFinished += OnFinished;
            }

            label.text = text;
            label.Play();
        }

        public void Skip(AnimatedLabel label)
        {
            label.Skip();
        }

        public void Dispose()
        {
            foreach (var settings in _presets.Values)
            {
                if (settings != null)
                    UnityEngine.Object.Destroy(settings);
            }
            _presets.Clear();
        }
    }
}
