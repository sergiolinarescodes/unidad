using TextAnimationsForUIToolkit;
using UnityEngine;

namespace Unidad.Core.UI.TextAnimation
{
    public static class TextAnimationPresets
    {
        public static TextAnimationSettings CreateDialogPreset()
        {
            var settings = ScriptableObject.CreateInstance<TextAnimationSettings>();
            settings.name = "DialogPreset";
            settings.enableTextAppearance = true;
            settings.baseAppearanceSpeed = 30f;
            settings.enableTextVanishing = false;
            settings.pauseAfterComma = 0.15f;
            settings.pauseAfterPunctuation = 0.3f;
            return settings;
        }

        public static TextAnimationSettings CreateFloatingPreset()
        {
            var settings = ScriptableObject.CreateInstance<TextAnimationSettings>();
            settings.name = "FloatingPreset";
            settings.enableTextAppearance = false;
            settings.enableTextVanishing = true;
            settings.baseVanishingSpeed = 20f;
            settings.vanishingDelay = 1.5f;
            return settings;
        }

        public static TextAnimationSettings CreateTitlePreset()
        {
            var settings = ScriptableObject.CreateInstance<TextAnimationSettings>();
            settings.name = "TitlePreset";
            settings.enableTextAppearance = true;
            settings.baseAppearanceSpeed = 15f;
            settings.enableTextVanishing = false;
            settings.pauseAfterComma = 0.2f;
            settings.pauseAfterPunctuation = 0.5f;
            return settings;
        }

        public static TextAnimationSettings CreateDefaultPreset()
        {
            var settings = ScriptableObject.CreateInstance<TextAnimationSettings>();
            settings.name = "DefaultPreset";
            settings.enableTextAppearance = false;
            settings.enableTextVanishing = false;
            return settings;
        }
    }
}
