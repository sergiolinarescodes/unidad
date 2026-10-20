using System;
using Unidad.Adapter.Motion;
using Unidad.Core.Abstractions;
using Unidad.Core.Motion;
using UnityEngine;
using NVector4 = System.Numerics.Vector4;
using UObject = UnityEngine.Object;

namespace Unidad.Adapter
{
    /// <summary>
    /// The scene's motions, ticked in Presentation with runWhilePaused (the bootstrap registers it), so paused motions
    /// that asked for it still move on unscaled time.
    /// </summary>
    internal sealed class MotionRunner : IMotionRunner, ITickable
    {
        private readonly ITickRegistry _ticks;
        private readonly ITimeProvider _time;
        private readonly MotionTable _table = new(new CoreMotionSampler());

        public MotionRunner(ITickRegistry ticks, ITimeProvider time)
        {
            _ticks = ticks;
            _time = time;
        }

        public bool SkipAll
        {
            get => _table.SkipAll;
            set => _table.SkipAll = value;
        }

        public int Active => _table.Active;

        public MotionHandle Play<T>(T target, IMotionTarget<T> adapter, Vector4 to, MotionSpec spec, string channel,
            MotionCurve curve = default, bool runWhilePaused = false, Action onComplete = null) where T : class
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            return _table.Start(target, channel, new Binding<T>(target, adapter, onComplete), ToNumerics(to), spec, curve, runWhilePaused);
        }

        public bool IsPlaying(MotionHandle handle) => _table.IsPlaying(handle);

        public bool IsPlaying(object owner, string channel) => _table.IsPlaying(owner, channel);

        public void Cancel(object owner, string channel) => _table.Cancel(owner, channel);

        public void Complete(object owner, string channel) => _table.Complete(owner, channel);

        public void CancelAll(object owner) => _table.CancelAll(owner);

        public void Tick(float deltaTime) => _table.Advance(_time.DeltaTime, _time.UnscaledDeltaTime, _ticks.Paused);

        private static NVector4 ToNumerics(Vector4 v) => new(v.x, v.y, v.z, v.w);

        // shortcut: one small allocation per Play; pool per T if a profile ever shows it
        private sealed class Binding<T> : IMotionBinding where T : class
        {
            private readonly T _target;
            private readonly IMotionTarget<T> _adapter;
            private readonly Action _onComplete;

            public Binding(T target, IMotionTarget<T> adapter, Action onComplete)
            {
                _target = target;
                _adapter = adapter;
                _onComplete = onComplete;
            }

            // a destroyed UnityEngine.Object compares equal to null through its overloaded operator
            public bool IsAlive => _target is not UObject unityObject || unityObject != null;

            public NVector4 Read() => ToNumerics(_adapter.Read(_target));

            public void Write(NVector4 value) => _adapter.Write(_target, new Vector4(value.X, value.Y, value.Z, value.W));

            public void Completed() => _onComplete?.Invoke();
        }
    }
}
