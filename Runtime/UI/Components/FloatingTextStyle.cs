namespace Unidad.Core.UI.Components
{
    public enum FloatingTextType
    {
        Info,
        Damage,
        Heal,
        Critical
    }

    public sealed class FloatingTextStyle
    {
        public FloatingTextType Type { get; }
        public string UssClass { get; }
        public string AnimationTag { get; }
        public float Duration { get; }

        private FloatingTextStyle(FloatingTextType type, string ussClass, string animationTag, float duration)
        {
            Type = type;
            UssClass = ussClass;
            AnimationTag = animationTag;
            Duration = duration;
        }

        public static readonly FloatingTextStyle Info = new(
            FloatingTextType.Info, "", "", 1.5f);

        public static readonly FloatingTextStyle Damage = new(
            FloatingTextType.Damage,
            "unidad-floating-text--damage",
            "<shake a=0.1 f=5>{0}</shake>",
            1.5f);

        public static readonly FloatingTextStyle Heal = new(
            FloatingTextType.Heal,
            "unidad-floating-text--heal",
            "<wave a=0.1 f=1>{0}</wave>",
            1.5f);

        public static readonly FloatingTextStyle Critical = new(
            FloatingTextType.Critical,
            "unidad-floating-text--critical",
            "<bounce a=0.3 f=2>{0}</bounce>",
            2f);

        public string FormatText(string text)
        {
            if (string.IsNullOrEmpty(AnimationTag))
                return text;
            return string.Format(AnimationTag, text);
        }
    }
}
