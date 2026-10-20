using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.Diagnostics;

namespace Unidad.Core.Ticking
{
    /// <summary>
    /// The engine-free heart of the tick pump: steps registered entries group by group
    /// (<see cref="TickGroup"/> order, then registration order), each either every step or at its
    /// own fixed rate.
    /// <para>
    /// A throwing entry is logged once and disabled; every other entry keeps running. Registering
    /// or unregistering during a step is safe: a new entry first runs on the next step, and an entry
    /// removed mid-step does not run again, even later in the same step.
    /// </para>
    /// </summary>
    public sealed class TickSchedule
    {
        public const TickGroup DefaultGroup = TickGroup.Simulation;

        /// <summary>At most this many catch-up steps per call for a fixed-rate entry; the rest of a long hitch is dropped.</summary>
        public const int MaxCatchUpSteps = 4;

        private readonly string _label;
        private readonly List<Entry> _entries = new();
        private readonly List<TickFault> _faults = new();
        private Entry[] _order = Array.Empty<Entry>();
        private bool _dirty;
        private long _sequence;

        public TickSchedule(string label = "TickSchedule")
        {
            _label = label;
        }

        /// <summary>Entries currently registered (faulted ones are gone).</summary>
        public int Count => _entries.Count;

        /// <summary>Every entry disabled by an exception, in the order they threw.</summary>
        public IReadOnlyList<TickFault> Faults => _faults;

        /// <summary>Told how long each entry ran, when set (a profiling readout); null costs one check per entry.</summary>
        public ITickProbe Probe { get; set; }

        public bool Add(ITickable tickable, TickGroup group = DefaultGroup, float rateHz = 0f)
        {
            if (tickable == null) throw new ArgumentNullException(nameof(tickable));
            return Add(tickable, tickable.Tick, group, rateHz, tickable.GetType().Name);
        }

        public bool Add(IFixedTickable tickable, TickGroup group = DefaultGroup)
        {
            if (tickable == null) throw new ArgumentNullException(nameof(tickable));
            return Add(tickable, tickable.FixedTick, group, 0f, tickable.GetType().Name);
        }

        /// <summary>
        /// Registers <paramref name="step"/> under <paramref name="key"/>. <paramref name="rateHz"/> of 0
        /// runs it every step with the step's delta; a positive rate runs it once per 1/rate seconds with
        /// exactly that interval. False if the key is already registered.
        /// </summary>
        public bool Add(object key, Action<float> step, TickGroup group, float rateHz, string name)
            => Add(key, step, group, rateHz, name, runWhilePaused: false);

        /// <summary>As the five-argument Add; a <paramref name="runWhilePaused"/> entry keeps running while paused, on the unscaled delta.</summary>
        public bool Add(object key, Action<float> step, TickGroup group, float rateHz, string name, bool runWhilePaused)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (float.IsNaN(rateHz) || rateHz < 0f)
                throw new ArgumentOutOfRangeException(nameof(rateHz), rateHz, "A tick rate is 0 (every frame) or positive.");
            if (IndexOf(key) >= 0) return false;

            _entries.Add(new Entry(key, step, group, rateHz > 0f ? 1.0 / rateHz : 0.0, name ?? key.GetType().Name, _sequence++,
                                   runWhilePaused));
            _dirty = true;
            return true;
        }

        /// <summary>Registers <paramref name="binding"/> keyed by its tickable.</summary>
        public bool Add(TickBinding binding)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            var tickable = binding.Tickable;
            return Add(tickable, tickable.Tick, binding.Group, binding.RateHz, tickable.GetType().Name, binding.RunWhilePaused);
        }

        public bool Remove(object key)
        {
            var index = IndexOf(key);
            if (index < 0) return false;
            _entries[index].Removed = true;
            _entries.RemoveAt(index);
            _dirty = true;
            return true;
        }

        public bool Contains(object key) => IndexOf(key) >= 0;

        /// <summary>Runs every entry once (or its due fixed-rate steps), group by group.</summary>
        public void Step(float deltaTime) => Step(deltaTime, deltaTime, false);

        /// <summary>
        /// Runs every group once. While <paramref name="paused"/>, only runWhilePaused entries run (an entry skipped keeps its
        /// accumulator); those always receive <paramref name="unscaledDeltaTime"/>, the rest <paramref name="deltaTime"/>.
        /// </summary>
        public void Step(float deltaTime, float unscaledDeltaTime, bool paused)
            => StepEntries(null, deltaTime, unscaledDeltaTime, paused);

        /// <summary>As <see cref="Step(float, float, bool)"/>, for the entries of one <paramref name="group"/> only (one PlayerLoop phase).</summary>
        public void StepGroup(TickGroup group, float deltaTime, float unscaledDeltaTime, bool paused)
            => StepEntries(group, deltaTime, unscaledDeltaTime, paused);

        private void StepEntries(TickGroup? only, float deltaTime, float unscaledDeltaTime, bool paused)
        {
            if (_dirty) Rebuild();
            var order = _order;
            for (var i = 0; i < order.Length; i++)
            {
                var entry = order[i];
                if (entry.Removed) continue;
                if (only.HasValue && entry.Group != only.Value) continue;
                if (paused && !entry.RunWhilePaused) continue;
                var delta = entry.RunWhilePaused ? unscaledDeltaTime : deltaTime;
                if (entry.Interval <= 0.0)
                {
                    Run(entry, delta);
                    continue;
                }

                entry.Accumulator += delta;
                var steps = 0;
                while (entry.Accumulator >= entry.Interval && steps < MaxCatchUpSteps && !entry.Removed)
                {
                    entry.Accumulator -= entry.Interval;
                    steps++;
                    Run(entry, (float)entry.Interval);
                }
                if (entry.Accumulator >= entry.Interval) entry.Accumulator %= entry.Interval;
            }
        }

        private void Run(Entry entry, float deltaTime)
        {
            try
            {
                var probe = Probe;
                if (probe == null)
                {
                    entry.Step(deltaTime);
                    return;
                }
                var start = System.Diagnostics.Stopwatch.GetTimestamp();
                entry.Step(deltaTime);
                probe.Ran(entry.Key, entry.Name, entry.Group, System.Diagnostics.Stopwatch.GetTimestamp() - start);
            }
            catch (Exception ex)
            {
                entry.Removed = true;
                _entries.Remove(entry);
                _dirty = true;
                _faults.Add(new TickFault(entry.Name, entry.Group, ex));
                Log.Error($"[{_label}] {entry.Name} threw in {entry.Group} and is disabled; the rest keep running. {ex.GetType().Name}: {ex.Message}");
                Log.Exception(ex);
            }
        }

        private int IndexOf(object key)
        {
            for (var i = 0; i < _entries.Count; i++)
                if (ReferenceEquals(_entries[i].Key, key)) return i;
            return -1;
        }

        private void Rebuild()
        {
            var order = _entries.ToArray();
            Array.Sort(order, (a, b) => a.Group != b.Group ? a.Group.CompareTo(b.Group) : a.Sequence.CompareTo(b.Sequence));
            _order = order;
            _dirty = false;
        }

        private sealed class Entry
        {
            public Entry(object key, Action<float> step, TickGroup group, double interval, string name, long sequence,
                         bool runWhilePaused)
            {
                Key = key; Step = step; Group = group; Interval = interval; Name = name; Sequence = sequence;
                RunWhilePaused = runWhilePaused;
            }

            public object Key { get; }
            public Action<float> Step { get; }
            public TickGroup Group { get; }
            public double Interval { get; }
            public string Name { get; }
            public long Sequence { get; }
            public bool RunWhilePaused { get; }
            public double Accumulator;
            public bool Removed;
        }
    }

    /// <summary>Receives each tick entry's run time in <see cref="System.Diagnostics.Stopwatch"/> ticks.</summary>
    public interface ITickProbe
    {
        void Ran(object key, string name, TickGroup group, long stopwatchTicks);
    }
}
