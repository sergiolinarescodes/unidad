namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Abstraction over the engine's frame clock. The engine adapter provides the production
    /// implementation; tests use <see cref="ManualTimeProvider"/> to control time precisely.
    /// NEVER read engine time directly in services — inject this instead.
    /// </summary>
    public interface ITimeProvider
    {
        float DeltaTime { get; }

        /// <summary>The frame's delta ignoring the time scale (a paused game still animates its pause menu).</summary>
        float UnscaledDeltaTime => DeltaTime;

        /// <summary>Seconds since start, single precision: loses milliseconds after a few hours.</summary>
        float Time { get; }

        float FixedDeltaTime { get; }
        int FrameCount { get; }

        /// <summary>Seconds since start, double precision. Long sessions and timestamps read this one.</summary>
        double PreciseTime => Time;
    }

    /// <summary>
    /// Test implementation with manual time control.
    /// Use in tests to step time deterministically.
    /// </summary>
    public sealed class ManualTimeProvider : ITimeProvider
    {
        private double _time;

        private float? _unscaledDeltaTime;

        public float DeltaTime { get; set; } = 1f / 60f;

        /// <summary>Equals <see cref="DeltaTime"/> until set.</summary>
        public float UnscaledDeltaTime
        {
            get => _unscaledDeltaTime ?? DeltaTime;
            set => _unscaledDeltaTime = value;
        }

        public float Time
        {
            get => (float)_time;
            set => _time = value;
        }

        public double PreciseTime
        {
            get => _time;
            set => _time = value;
        }

        public float FixedDeltaTime { get; set; } = 0.02f;
        public int FrameCount { get; set; }

        public void Advance(float dt)
        {
            DeltaTime = dt;
            _time += dt;
            FrameCount++;
        }
    }
}
