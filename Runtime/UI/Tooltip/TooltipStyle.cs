using Unidad.Core.UI.DesignSystem;
using UnityEngine;

namespace Unidad.Core.UI.Tooltip
{
    public sealed class TooltipStyle
    {
        // Dark grey, 70% opaque — the shared GRAVE INTENT tooltip background (semi-transparent so the scene reads through).
        public Color BackgroundColor { get; init; } = new(0.10f, 0.10f, 0.10f, 0.70f);
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

        // Info shares the one tooltip look now (dark grey bg + injected frame); kept as a named alias for call sites.
        public static readonly TooltipStyle Info = new();

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
