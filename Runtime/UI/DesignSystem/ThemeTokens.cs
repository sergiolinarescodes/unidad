using UnityEngine;

namespace Unidad.Core.UI.DesignSystem
{
    public static class ThemeTokens
    {
        // Colors — Primary
        public static readonly Color Primary = new(0.29f, 0.56f, 0.85f);       // #4A90D9
        public static readonly Color Secondary = new(0.48f, 0.41f, 0.93f);     // #7B68EE
        public static readonly Color Success = new(0.30f, 0.69f, 0.31f);       // #4CAF50
        public static readonly Color Warning = new(1.00f, 0.60f, 0.00f);       // #FF9800
        public static readonly Color Error = new(0.96f, 0.26f, 0.21f);         // #F44336

        // Colors — Background
        public static readonly Color BgPrimary = new(0.10f, 0.10f, 0.18f);     // #1A1A2E
        public static readonly Color BgSecondary = new(0.09f, 0.13f, 0.24f);   // #16213E
        public static readonly Color BgSurface = new(0.06f, 0.20f, 0.38f);     // #0F3460

        // Colors — Text
        public static readonly Color TextPrimary = Color.white;
        public static readonly Color TextSecondary = new(0.69f, 0.69f, 0.69f); // #B0B0B0
        public static readonly Color TextMuted = new(0.40f, 0.40f, 0.40f);     // #666666

        // Typography (2x scale)
        public const int FontSizeXs = 20;
        public const int FontSizeSm = 24;
        public const int FontSizeMd = 32;
        public const int FontSizeLg = 40;
        public const int FontSizeXl = 56;
        public const int FontSizeXxl = 72;

        // Spacing
        public const int SpacingXs = 4;
        public const int SpacingSm = 8;
        public const int SpacingMd = 16;
        public const int SpacingLg = 24;
        public const int SpacingXl = 32;

        // Borders & Radius
        public const int RadiusSm = 4;
        public const int RadiusMd = 8;
        public const int RadiusLg = 16;
        public const int BorderWidth = 1;

        // Transitions (ms)
        public const int TransitionFast = 150;
        public const int TransitionNormal = 300;
        public const int TransitionSlow = 500;
    }
}
