namespace Unidad.Core.Motion
{
    /// <summary>A scale punch: 1 rises to <paramref name="Peak"/> over Up, holds for Hold, falls back to 1 over Down.</summary>
    public readonly record struct PunchSpec(float Peak, float Up, float Hold, float Down, Ease UpEase = Ease.OutQuad, Ease DownEase = Ease.InOutQuad)
    {
        public float Duration => Up + Hold + Down;

        /// <summary>The moment of contact: the end of the rise.</summary>
        public float PeakTime => Up;

        /// <summary>The scale factor at <paramref name="t"/>; 1 outside the punch.</summary>
        public float Sample(float t)
        {
            if (t < 0f) return 1f;
            if (t < Up) return Easing.Lerp(1f, Peak, Easing.Evaluate(UpEase, t / Up));
            if (t < Up + Hold) return Peak;
            if (t < Duration) return Easing.Lerp(Peak, 1f, Easing.Evaluate(DownEase, (t - Up - Hold) / Down));
            return 1f;
        }
    }
}
