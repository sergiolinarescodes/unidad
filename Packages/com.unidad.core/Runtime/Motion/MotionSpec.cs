using System;

namespace Unidad.Core.Motion
{
    /// <summary>How one motion runs: its length, its curve and its start delay, in seconds.</summary>
    public readonly record struct MotionSpec(float Duration, Ease Ease = Ease.Linear, float Delay = 0f, float Overshoot = Easing.DefaultOvershoot)
    {
        public float End => Delay + Duration;

        /// <summary>Eased progress 0..1 at <paramref name="elapsed"/>: 0 before the delay; a zero duration gives 1 once the delay passed.</summary>
        public float Progress(float elapsed) => throw new NotImplementedException();
    }
}
