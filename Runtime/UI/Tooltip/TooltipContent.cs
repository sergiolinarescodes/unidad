using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Tooltip
{
    public sealed class TooltipContent
    {
        public string Text { get; }
        public Func<VisualElement> CustomBuilder { get; }
        public bool IsCustom => CustomBuilder != null;
        public IReadOnlyList<SubTooltipEntry> SubTooltips { get; }

        private TooltipContent(string text, Func<VisualElement> customBuilder,
            IReadOnlyList<SubTooltipEntry> subTooltips = null)
        {
            Text = text;
            CustomBuilder = customBuilder;
            SubTooltips = subTooltips ?? Array.Empty<SubTooltipEntry>();
        }

        public static TooltipContent FromText(string text) => new(text, null);

        public static TooltipContent FromCustom(Func<VisualElement> builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return new(null, builder);
        }

        public TooltipContent WithSubTooltips(params SubTooltipEntry[] entries)
            => new(Text, CustomBuilder, entries);

        public TooltipContent WithSubTooltips(IReadOnlyList<SubTooltipEntry> entries)
            => new(Text, CustomBuilder, entries);
    }
}
