using System;
using Unidad.Core.Abstractions;

namespace Unidad.Adapter
{
    /// <summary>The engine clocks: deltaTime, unscaledDeltaTime, time, timeAsDouble, fixedDeltaTime, frameCount.</summary>
    public sealed class UnityTimeProvider : ITimeProvider
    {
        public float DeltaTime => throw new NotImplementedException();

        public float UnscaledDeltaTime => throw new NotImplementedException();

        public float Time => throw new NotImplementedException();

        public double PreciseTime => throw new NotImplementedException();

        public float FixedDeltaTime => throw new NotImplementedException();

        public int FrameCount => throw new NotImplementedException();
    }
}
