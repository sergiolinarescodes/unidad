using System;
using System.Collections.Generic;
using TextAnimationsForUIToolkit;
using TextAnimationsForUIToolkit.Events;

namespace Unidad.Core.UI.TextAnimation
{
    internal sealed class TextAnimationService : ITextAnimationService, IDisposable
    {
        private readonly Dictionary<string, TextAnimationSettings> _presets = new();
        private readonly Dictionary<string, TextAnimationRecipe> _recipes = new();

        public TextAnimationSettings DefaultSettings { get; }

        public TextAnimationService(TextAnimationSettings defaultSettings = null)
        {
            DefaultSettings = defaultSettings ?? TextAnimationPresets.CreateDefaultPreset();
            _presets["default"] = DefaultSettings;
            _presets["dialog"] = TextAnimationPresets.CreateDialogPreset();
            _presets["floating"] = TextAnimationPresets.CreateFloatingPreset();
            _presets["title"] = TextAnimationPresets.CreateTitlePreset();

            _recipes["damage"] = new TextAnimationRecipe("<shake a=0.1 f=5>{0}</shake>", 1.5f);
            _recipes["heal"] = new TextAnimationRecipe("<wave a=0.1 f=1>{0}</wave>", 1.5f);
            _recipes["critical"] = new TextAnimationRecipe("<bounce a=0.3 f=2>{0}</bounce>", 2.0f);
            _recipes["editor-open"] = new TextAnimationRecipe("<wave a=0.15 f=2.0 w=15 l=1>{0}</wave>", 1.0f);
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

        public void RegisterRecipe(string name, TextAnimationRecipe recipe)
        {
            _recipes[name] = recipe;
        }

        public TextAnimationRecipe GetRecipe(string name)
        {
            return _recipes.TryGetValue(name, out var recipe) ? recipe : null;
        }

        public string ApplyRecipe(string recipeName, string text)
        {
            if (string.IsNullOrEmpty(recipeName))
                return text;
            return GetRecipe(recipeName)?.Apply(text) ?? text;
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
