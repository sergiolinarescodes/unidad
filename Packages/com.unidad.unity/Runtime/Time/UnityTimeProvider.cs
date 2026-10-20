using Unidad.Core.Abstractions;
using EngineTime = UnityEngine.Time;

namespace Unidad.Adapter
{
    /// <summary>The engine clocks: deltaTime, unscaledDeltaTime, time, timeAsDouble, fixedDeltaTime, frameCount.</summary>
    public sealed class UnityTimeProvider : ITimeProvider
    {
        public float DeltaTime => EngineTime.deltaTime;

        public float UnscaledDeltaTime => EngineTime.unscaledDeltaTime;

        public float Time => EngineTime.time;

        public double PreciseTime => EngineTime.timeAsDouble;

        public float FixedDeltaTime => EngineTime.fixedDeltaTime;

        public int FrameCount => EngineTime.frameCount;
    }
}
