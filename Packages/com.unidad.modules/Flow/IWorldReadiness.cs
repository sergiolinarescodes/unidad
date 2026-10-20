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
        private readonly Func<bool> _busy;
        private readonly int _needed;
        private int _quiet;

        public SettledReadiness(Func<bool> busy, int settledTicks = FlowTuning.SettledTicks)
        {
            _busy = busy ?? throw new ArgumentNullException(nameof(busy));
            _needed = Math.Max(1, settledTicks);
        }

        public bool IsReady => _quiet >= _needed;

        public void Tick(float deltaTime)
        {
            if (IsReady) return;
            _quiet = _busy() ? 0 : _quiet + 1;
        }
    }

    /// <summary>Streaming may time out; required resources may not. Shared by every production scene.</summary>
    public sealed class RequiredReadiness : IWorldReadiness
    {
        private readonly IWorldReadiness _world;
        private readonly Func<bool> _prepared;

        public RequiredReadiness(IWorldReadiness world, Func<bool> prepared)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _prepared = prepared ?? throw new ArgumentNullException(nameof(prepared));
        }

        public bool IsReady => _prepared() && _world.IsReady;

        public bool CanTimeout => _prepared() && _world.CanTimeout;
    }
}
