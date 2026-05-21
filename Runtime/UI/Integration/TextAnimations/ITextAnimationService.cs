using System;
using TextAnimationsForUIToolkit;

namespace Unidad.Core.UI.TextAnimation
{
    public interface ITextAnimationService
    {
        TextAnimationSettings DefaultSettings { get; }
        TextAnimationSettings GetPreset(string presetName);
        void RegisterPreset(string presetName, TextAnimationSettings settings);

        AnimatedLabel CreateLabel(string preset = "default");
        AnimatedButton CreateButton(string preset = "default");

        void PlayTypewriter(AnimatedLabel label, string text, Action onComplete = null);
        void Skip(AnimatedLabel label);

        void RegisterRecipe(string name, TextAnimationRecipe recipe);
        TextAnimationRecipe GetRecipe(string name);
        string ApplyRecipe(string recipeName, string text);
    }
}
