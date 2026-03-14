using Unidad.Core.UI.DesignSystem;
using UnityEngine;

namespace Unidad.Core.UI.Tooltip
{
    public sealed class TooltipStyle
    {
        public Color BackgroundColor { get; init; } = new(0.086f, 0.129f, 0.243f, 0.95f);
        public Color BorderColor { get; init; } = new(1f, 1f, 1f, 0.15f);
        public int BorderWidth { get; init; } = ThemeTokens.BorderWidth;
        public int BorderRadius { get; init; } = ThemeTokens.RadiusMd;
        public int PaddingH { get; init; } = ThemeTokens.SpacingMd;
        public int PaddingV { get; init; } = ThemeTokens.SpacingSm;
        public Color TextColor { get; init; } = Color.white;
        public int FontSize { get; init; } = ThemeTokens.FontSizeSm;
        public bool ShowArrow { get; init; } = true;
        public int ArrowSize { get; init; } = 8;
        public int MaxWidth { get; init; } = 300;
        public float FadeInDuration { get; init; } = 0.15f;
        public float FadeOutDuration { get; init; } = 0.1f;
        public float SubTooltipDelayMs { get; init; } = 1000f;
        public int SubTooltipGap { get; init; } = 4;

        public static readonly TooltipStyle Default = new();

        public static readonly TooltipStyle Info = new()
        {
            BackgroundColor = new Color(0.18f, 0.30f, 0.55f, 0.95f),
            BorderColor = new Color(0.29f, 0.56f, 0.85f, 0.4f)
        };

        public static readonly TooltipStyle Minimal = new()
        {
            ShowArrow = false,
            PaddingH = ThemeTokens.SpacingSm,
            PaddingV = ThemeTokens.SpacingXs,
            FontSize = ThemeTokens.FontSizeXs,
            BorderRadius = ThemeTokens.RadiusSm
        };
    }
}
