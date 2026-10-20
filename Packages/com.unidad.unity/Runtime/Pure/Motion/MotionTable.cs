using System.Collections.Generic;
using System.Numerics;
using Unidad.Adapter.Motion;
using Unidad.Core.Motion;

namespace Unidad.Adapter
{
    /// <summary>
    /// Every running motion, by slot. One live motion per (owner, channel): a start on a busy pair cancels the old one
    /// without its callback, and that is the only cancellation a start causes.
    /// </summary>
    internal sealed class MotionTable
    {
        private struct Slot
        {
            public int Generation;
            public bool Live;
            public object Owner;
            public string Channel;
            public IMotionBinding Binding;
            public Vector4 From;
            public Vector4 To;
            public MotionSpec Spec;
            public MotionCurve Curve;
            public float Elapsed;
            public bool RunWhilePaused;
            public int StartedOnStep;
        }

        private readonly IMotionSampler _sampler;
        private readonly List<Slot> _slots = new();
        private readonly Stack<int> _free = new();
        private int _step;
        private bool _advancing;

        public MotionTable(IMotionSampler sampler) => _sampler = sampler;

        /// <summary>anim.skip: every start completes inside <see cref="Start"/>.</summary>
        public bool SkipAll { get; set; }

        public int Active { get; private set; }

        public MotionHandle Start(object owner, string channel, IMotionBinding binding, Vector4 to, MotionSpec spec,
            MotionCurve curve, bool runWhilePaused)
        {
            Cancel(owner, channel);
            var from = binding.Read();
            if (SkipAll)
            {
                binding.Write(curve.EndsAtFrom ? from : to);
                binding.Completed();
                return MotionHandle.None;
            }

            var index = _free.Count > 0 ? _free.Pop() : Grow();
            var slot = _slots[index];
            slot.Generation++;
            slot.Live = true;
            slot.Owner = owner;
            slot.Channel = channel;
            slot.Binding = binding;
            slot.From = from;
            slot.To = to;
            slot.Spec = spec;
            slot.Curve = curve;
            slot.Elapsed = 0f;
            slot.RunWhilePaused = runWhilePaused;
            slot.StartedOnStep = _advancing ? _step : -1;
            _slots[index] = slot;
            Active++;
            return new MotionHandle(index, slot.Generation);
        }

        public bool IsPlaying(MotionHandle handle) =>
            handle.Generation != 0 && handle.Slot >= 0 && handle.Slot < _slots.Count &&
            _slots[handle.Slot].Live && _slots[handle.Slot].Generation == handle.Generation;

        public bool IsPlaying(object owner, string channel) => Find(owner, channel) >= 0;

        /// <summary>The value stays where it is; no callback.</summary>
        public void Cancel(object owner, string channel)
        {
            var index = Find(owner, channel);
            if (index >= 0) Free(index);
        }

        /// <summary>Writes the end value, then calls the callback.</summary>
        public void Complete(object owner, string channel)
        {
            var index = Find(owner, channel);
            if (index >= 0) Finish(index);
        }

        public void CancelAll(object owner)
        {
            for (var i = 0; i < _slots.Count; i++)
                if (_slots[i].Live && ReferenceEquals(_slots[i].Owner, owner)) Free(i);
        }

        /// <summary>One frame. Slots started during this call first move on the next one.</summary>
        public void Advance(float scaledDelta, float unscaledDelta, bool paused)
        {
            _step++;
            _advancing = true;
            try
            {
                for (var i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];
                    if (!slot.Live || slot.StartedOnStep == _step) continue;
                    if (!slot.Binding.IsAlive)
                    {
                        Free(i);
                        continue;
                    }

                    if (paused && !slot.RunWhilePaused) continue;
                    slot.Elapsed += slot.RunWhilePaused ? unscaledDelta : scaledDelta;
                    _slots[i] = slot;
                    if (slot.Elapsed >= slot.Spec.End) Finish(i);
                    else if (slot.Elapsed >= slot.Spec.Delay)
                        slot.Binding.Write(_sampler.Sample(slot.Curve, slot.From, slot.To, slot.Spec, slot.Elapsed));
                }
            }
            finally
            {
                _advancing = false;
            }
        }

        private int Find(object owner, string channel)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Live && ReferenceEquals(slot.Owner, owner) && string.Equals(slot.Channel, channel, System.StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private void Finish(int index)
        {
            var slot = _slots[index];
            slot.Binding.Write(slot.Curve.EndsAtFrom ? slot.From : slot.To);
            Free(index);
            slot.Binding.Completed();
        }

        private void Free(int index)
        {
            var slot = _slots[index];
            slot.Live = false;
            slot.Owner = null;
            slot.Channel = null;
            slot.Binding = null;
            _slots[index] = slot;
            _free.Push(index);
            Active--;
        }

        private int Grow()
        {
            _slots.Add(default);
            return _slots.Count - 1;
        }
    }
}
