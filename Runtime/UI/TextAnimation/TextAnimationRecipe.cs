namespace Unidad.Core.UI.TextAnimation
{
    public sealed class TextAnimationRecipe
    {
        public string MarkupTemplate { get; }
        public float Duration { get; }

        public TextAnimationRecipe(string markupTemplate, float duration)
        {
            MarkupTemplate = markupTemplate;
            Duration = duration;
        }

        public string Apply(string text) => string.IsNullOrEmpty(MarkupTemplate)
            ? text
            : string.Format(MarkupTemplate, text);
    }
}
