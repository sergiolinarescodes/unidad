using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.Ticking;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>Where a scene registers what it ticks; pause is core's (only runWhilePaused entries run, on unscaled time).</summary>
    public interface ITickRegistry
    {
        bool Paused { get; set; }

        bool Register(ITickable tickable, TickGroup group, float rateHz = 0f, bool runWhilePaused = false);

        bool Register(TickBinding binding);

        bool RegisterFixed(IFixedTickable tickable, bool runWhilePaused = false);

        bool Unregister(object tickable);

        bool IsRegistered(object tickable);

        IReadOnlyList<TickFault> Faults { get; }
    }

    /// <summary>
    /// The tick pump: four PlayerLoop systems (PreUpdate after the Input System, Update, PreLateUpdate, FixedUpdate) step four
    /// TickSchedules. Inserted on enable, removed on disable, and stripped by marker name on reload.
    /// </summary>
    [ProcessOverride("The tick pump: PlayerLoop systems step the four TickSchedules.")]
    public sealed class TickRunner : MonoBehaviour, ITickRegistry
    {
        public bool Paused
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public IReadOnlyList<TickFault> Faults => throw new NotImplementedException();

        public bool Register(ITickable tickable, TickGroup group, float rateHz = 0f, bool runWhilePaused = false) => throw new NotImplementedException();

        public bool Register(TickBinding binding) => throw new NotImplementedException();

        public bool RegisterFixed(IFixedTickable tickable, bool runWhilePaused = false) => throw new NotImplementedException();

        public bool Unregister(object tickable) => throw new NotImplementedException();

        public bool IsRegistered(object tickable) => throw new NotImplementedException();
    }

    /// <summary>
    /// The PlayerLoop marker types. Tools strip every system whose type FullName starts with "Unidad.Adapter.UnidadTick",
    /// so these names are a contract.
    /// </summary>
    public static class UnidadTick
    {
        public struct Input { }
        public struct Update { }
        public struct LateUpdate { }
        public struct Fixed { }
    }
}
