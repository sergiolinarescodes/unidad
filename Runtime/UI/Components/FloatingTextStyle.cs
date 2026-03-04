using UnityEngine;

namespace Unidad.Core.UI.Components
{
    public enum FloatingTextType
    {
        Info,
        Damage,
        Heal,
        Critical,
        EnergyCost
    }

    public sealed class FloatingTextStyle
    {
        public FloatingTextType Type { get; }
        public string UssClass { get; }
        public string AnimationTag { get; }
        public float Duration { get; }
        public Color Color { get; }
        public int FontSize { get; }
        public Vector3 DriftOffset { get; }
        public bool FadeOut { get; }
        public float WorldScale { get; }

        public FloatingTextStyle(
            FloatingTextType type, string ussClass, string animationTag, float duration,
            Color color, int fontSize, Vector3 driftOffset, bool fadeOut, float worldScale)
        {
            Type = type;
            UssClass = ussClass;
            AnimationTag = animationTag;
            Duration = duration;
            Color = color;
            FontSize = fontSize;
            DriftOffset = driftOffset;
            FadeOut = fadeOut;
            WorldScale = worldScale;
        }

        private FloatingTextStyle(FloatingTextType type, string ussClass, string animationTag, float duration)
            : this(type, ussClass, animationTag, duration,
                   Color.white, 24, Vector3.up * 0.5f, true, 0.01f) { }

        private FloatingTextStyle(FloatingTextType type, string ussClass, string animationTag, float duration,
                                  Color color, int fontSize)
            : this(type, ussClass, animationTag, duration,
                   color, fontSize, Vector3.up * 0.5f, true, 0.01f) { }

        public static readonly FloatingTextStyle Info = new(
            FloatingTextType.Info, "", "", 1.5f);

        public static readonly FloatingTextStyle Damage = new(
            FloatingTextType.Damage,
            "unidad-floating-text--damage",
            "<shake a=0.1 f=5>{0}</shake>",
            1.5f,
            new Color(1f, 0.2f, 0.2f), 24);

        public static readonly FloatingTextStyle Heal = new(
            FloatingTextType.Heal,
            "unidad-floating-text--heal",
            "<wave a=0.1 f=1>{0}</wave>",
            1.5f,
            new Color(0.2f, 1f, 0.3f), 24);

        public static readonly FloatingTextStyle Critical = new(
            FloatingTextType.Critical,
            "unidad-floating-text--critical",
            "<bounce a=0.3 f=2>{0}</bounce>",
            2f,
            new Color(1f, 1f, 0.2f), 32);

        public static readonly FloatingTextStyle EnergyCost = new(
            FloatingTextType.EnergyCost,
            "unidad-floating-text--energy",
            "",
            0.8f,
            new Color(1f, 0.6f, 0.1f), 70, Vector3.up * 0.5f, true, 0.02f);

        public string FormatText(string text)
        {
            if (string.IsNullOrEmpty(AnimationTag))
                return text;
            return string.Format(AnimationTag, text);
        }
    }
}
