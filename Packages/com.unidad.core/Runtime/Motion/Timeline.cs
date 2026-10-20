using System;
using System.Collections.Generic;

namespace Unidad.Core.Motion
{
    /// <summary>
    /// Beats on a time axis: the before / on contact / after of a beat sheet. A beat fires once its time is reached, in
    /// (time, insertion) order; a beat added at or before <see cref="Time"/> fires on the next <see cref="Advance"/>.
    /// </summary>
    public sealed class Timeline<TBeat>
    {
        // Fired beats are [0, _next); the unfired tail stays sorted by (at, insertion).
        private readonly List<(float At, TBeat Beat)> _beats = new();
        private readonly List<TBeat> _fired = new();
        private int _next;

        /// <summary>Adds <paramref name="beat"/> at <paramref name="at"/> seconds (&gt;= 0; NaN or negative throws).</summary>
        public void Add(float at, TBeat beat)
        {
            if (!(at >= 0f)) throw new ArgumentOutOfRangeException(nameof(at), at, "A beat's time is >= 0.");
            int index = _beats.Count;
            while (index > _next && _beats[index - 1].At > at) index--;
            _beats.Insert(index, (at, beat));
            if (at > End) End = at;
        }

        public float Time { get; private set; }

        /// <summary>The time of the last beat.</summary>
        public float End { get; private set; }

        /// <summary>True when no beat is left to fire.</summary>
        public bool IsDone => _next == _beats.Count;

        public int Count => _beats.Count;

        /// <summary>The beats this step fired, in order; the list is reused by the next call.</summary>
        public IReadOnlyList<TBeat> Advance(float deltaTime)
        {
            if (!(deltaTime >= 0f)) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "A step is >= 0.");
            Time += deltaTime;
            _fired.Clear();
            while (_next < _beats.Count && _beats[_next].At <= Time) _fired.Add(_beats[_next++].Beat);
            return _fired;
        }

        /// <summary>Fires every remaining beat (anim.skip); <see cref="Time"/> becomes <see cref="End"/>.</summary>
        public IReadOnlyList<TBeat> Complete()
        {
            _fired.Clear();
            while (_next < _beats.Count) _fired.Add(_beats[_next++].Beat);
            if (End > Time) Time = End;
            return _fired;
        }

        /// <summary>Drops every beat; <see cref="Time"/> becomes 0.</summary>
        public void Clear()
        {
            _beats.Clear();
            _fired.Clear();
            _next = 0;
            Time = 0f;
            End = 0f;
        }
    }
}
