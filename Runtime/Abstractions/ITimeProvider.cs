namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Abstraction over Unity's Time class.
    /// Production implementation reads UnityEngine.Time.
    /// Tests use a manual implementation to control time precisely.
    /// NEVER read UnityEngine.Time directly in services — inject this instead.
    /// </summary>
    public interface ITimeProvider
    {
        float DeltaTime { get; }
        float Time { get; }
        float FixedDeltaTime { get; }
        int FrameCount { get; }
    }

    /// <summary>
    /// Production implementation that reads from UnityEngine.Time.
    /// Registered as singleton in UnidadBootstrap.
    /// </summary>
    internal sealed class UnityTimeProvider : ITimeProvider
    {
        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float Time => UnityEngine.Time.time;
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
        public int FrameCount => UnityEngine.Time.frameCount;
    }

    /// <summary>
    /// Test implementation with manual time control.
    /// Use in tests to step time deterministically.
    /// </summary>
    public sealed class ManualTimeProvider : ITimeProvider
    {
        public float DeltaTime { get; set; } = 1f / 60f;
        public float Time { get; set; }
        public float FixedDeltaTime { get; set; } = 0.02f;
        public int FrameCount { get; set; }

        public void Advance(float dt)
        {
            DeltaTime = dt;
            Time += dt;
            FrameCount++;
        }
    }
}
