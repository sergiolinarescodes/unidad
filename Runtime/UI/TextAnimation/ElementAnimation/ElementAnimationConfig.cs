namespace Unidad.Core.UI.TextAnimation.ElementAnimation
{
    public enum ElementAnimationType
    {
        SlideIn,
        SlideOut,
        FadeIn,
        FadeOut,
        ScaleIn,
        ScaleOut,
        Shake
    }

    public enum SlideDirection
    {
        Left,
        Right,
        Top,
        Bottom
    }

    public enum EasingMode
    {
        Linear,
        EaseInCubic,
        EaseOutCubic,
        EaseInOutCubic
    }

    public readonly record struct ElementAnimationConfig(
        ElementAnimationType Type,
        float Duration = 0.3f,
        SlideDirection Direction = SlideDirection.Bottom,
        EasingMode Easing = EasingMode.EaseOutCubic
    );
}
