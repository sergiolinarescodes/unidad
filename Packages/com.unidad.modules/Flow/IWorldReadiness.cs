using System;
using Unidad.Core.Abstractions;

namespace Unidad.Flow
{
    /// <summary>
    /// Whether the scene behind the curtain is ready to be seen. The flow leaves Loading when this turns
    /// true. A scene that streams content in reports it built and settled.
    /// </summary>
    public interface IWorldReadiness
    {
        bool IsReady { get; }
        bool CanTimeout => true;
    }

    /// <summary>Ready from the start: a scene with nothing to stream.</summary>
    public sealed class ImmediateReadiness : IWorldReadiness
    {
        public static ImmediateReadiness Instance { get; } = new();

        public bool IsReady => true;
    }

    /// <summary>
    /// Ready once <c>busy</c> has read false for <see cref="FlowTuning.SettledTicks"/> ticks in a row,
    /// so a one-frame lull between two streaming jobs is not mistaken for a settled world. Tick it after
    /// whatever it watches.
    /// </summary>
    public sealed class SettledReadiness : IWorldReadiness, ITickable
    {
        public SettledReadiness(Func<bool> busy, int settledTicks = FlowTuning.SettledTicks) => throw new NotImplementedException();

        public bool IsReady => throw new NotImplementedException();

        public void Tick(float deltaTime) => throw new NotImplementedException();
    }

    /// <summary>Streaming may time out; required resources may not. Shared by every production scene.</summary>
    public sealed class RequiredReadiness : IWorldReadiness
    {
        public RequiredReadiness(IWorldReadiness world, Func<bool> prepared) => throw new NotImplementedException();

        public bool IsReady => throw new NotImplementedException();

        public bool CanTimeout => throw new NotImplementedException();
    }
}
