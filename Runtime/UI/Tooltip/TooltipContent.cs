using System;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Tooltip
{
    public sealed class TooltipContent
    {
        public string Text { get; }
        public Func<VisualElement> CustomBuilder { get; }
        public bool IsCustom => CustomBuilder != null;

        private TooltipContent(string text, Func<VisualElement> customBuilder)
        {
            Text = text;
            CustomBuilder = customBuilder;
        }

        public static TooltipContent FromText(string text) => new(text, null);

        public static TooltipContent FromCustom(Func<VisualElement> builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return new(null, builder);
        }
    }
}
