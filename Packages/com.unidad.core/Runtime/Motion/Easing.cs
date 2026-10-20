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
        public static float Evaluate(Ease ease, float t, float overshoot = DefaultOvershoot)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            float s = overshoot;
            switch (ease)
            {
                case Ease.Linear: return t;
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Pow2(-2f * t + 2f) / 2f;
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1f - Pow3(1f - t);
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Pow3(-2f * t + 2f) / 2f;
                case Ease.InBack: return (s + 1f) * t * t * t - s * t * t;
                case Ease.OutBack: return 1f + (s + 1f) * Pow3(t - 1f) + s * Pow2(t - 1f);
                case Ease.InOutBack:
                {
                    float c = s * 1.525f;
                    return t < 0.5f
                        ? Pow2(2f * t) * ((c + 1f) * 2f * t - c) / 2f
                        : (Pow2(2f * t - 2f) * ((c + 1f) * (2f * t - 2f) + c) + 2f) / 2f;
                }
                case Ease.InSine: return 1f - MathF.Cos(t * MathF.PI / 2f);
                case Ease.OutSine: return MathF.Sin(t * MathF.PI / 2f);
                case Ease.InOutSine: return -(MathF.Cos(MathF.PI * t) - 1f) / 2f;
                case Ease.OutBounce: return OutBounce(t);
                default: throw new ArgumentOutOfRangeException(nameof(ease), ease, null);
            }
        }

        /// <summary>a + (b - a) * t, unclamped.</summary>
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Pow2(float x) => x * x;

        private static float Pow3(float x) => x * x * x;

        private static float OutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
