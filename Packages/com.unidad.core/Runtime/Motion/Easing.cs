using System;

namespace Unidad.Core.Motion
{
    /// <summary>Penner easing curves. The values are frozen: content and tunings name them.</summary>
    public enum Ease : byte
    {
        Linear = 0,
        InQuad = 1,
        OutQuad = 2,
        InOutQuad = 3,
        InCubic = 4,
        OutCubic = 5,
        InOutCubic = 6,
        InBack = 7,
        OutBack = 8,
        InOutBack = 9,
        InSine = 10,
        OutSine = 11,
        InOutSine = 12,
        OutBounce = 13,
    }

    public static class Easing
    {
        /// <summary>The Back curves' overshoot (DOTween's and Penner's default).</summary>
        public const float DefaultOvershoot = 1.70158f;

        /// <summary><paramref name="ease"/> at <paramref name="t"/>, clamped to [0, 1]; 0 at 0 and 1 at 1 for every curve.</summary>
        public static float Evaluate(Ease ease, float t, float overshoot = DefaultOvershoot) => throw new NotImplementedException();

        /// <summary>a + (b - a) * t, unclamped.</summary>
        public static float Lerp(float a, float b, float t) => throw new NotImplementedException();
    }
}
