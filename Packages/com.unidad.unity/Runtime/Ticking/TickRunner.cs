using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.Ticking;
using UnityEngine;
using EngineTime = UnityEngine.Time;

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
        private readonly TickSchedule _input = new("TickRunner.Input");
        private readonly TickSchedule _update = new("TickRunner.Update");
        private readonly TickSchedule _late = new("TickRunner.Late");
        private readonly TickSchedule _fixed = new("TickRunner.Fixed");

        public bool Paused { get; set; }

        public IReadOnlyList<TickFault> Faults
        {
            get
            {
                var all = new List<TickFault>(_input.Faults);
                all.AddRange(_update.Faults);
                all.AddRange(_late.Faults);
                all.AddRange(_fixed.Faults);
                return all;
            }
        }

        /// <summary>False when the tickable is already registered in any schedule.</summary>
        public bool Register(ITickable tickable, TickGroup group, float rateHz = 0f, bool runWhilePaused = false)
        {
            if (tickable == null) throw new ArgumentNullException(nameof(tickable));
            if (IsRegistered(tickable)) return false;
            return ScheduleFor(group).Add(tickable, tickable.Tick, group, rateHz, tickable.GetType().Name, runWhilePaused);
        }

        public bool Register(TickBinding binding)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            return Register(binding.Tickable, binding.Group, binding.RateHz, binding.RunWhilePaused);
        }

        public bool RegisterFixed(IFixedTickable tickable, bool runWhilePaused = false)
        {
            if (tickable == null) throw new ArgumentNullException(nameof(tickable));
            if (IsRegistered(tickable)) return false;
            return _fixed.Add(tickable, tickable.FixedTick, TickGroup.Fixed, 0f, tickable.GetType().Name, runWhilePaused);
        }

        public bool Unregister(object tickable) =>
            tickable != null && (_input.Remove(tickable) | _update.Remove(tickable) | _late.Remove(tickable) | _fixed.Remove(tickable));

        public bool IsRegistered(object tickable) =>
            tickable != null && (_input.Contains(tickable) || _update.Contains(tickable) || _late.Contains(tickable) || _fixed.Contains(tickable));

        internal void StepInput() => _input.Step(EngineTime.deltaTime, EngineTime.unscaledDeltaTime, Paused);

        internal void StepUpdate() => _update.Step(EngineTime.deltaTime, EngineTime.unscaledDeltaTime, Paused);

        internal void StepLate() => _late.Step(EngineTime.deltaTime, EngineTime.unscaledDeltaTime, Paused);

        internal void StepFixed() => _fixed.Step(EngineTime.fixedDeltaTime, EngineTime.fixedDeltaTime, Paused);

        private void OnEnable() => PlayerLoopInstaller.Insert(this);

        private void OnDisable() => PlayerLoopInstaller.Remove();

        private TickSchedule ScheduleFor(TickGroup group) => group switch
        {
            TickGroup.Input => _input,
            TickGroup.Simulation or TickGroup.Streaming => _update,
            TickGroup.Presentation or TickGroup.Rendering => _late,
            TickGroup.Fixed => _fixed,
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown tick group."),
        };
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
