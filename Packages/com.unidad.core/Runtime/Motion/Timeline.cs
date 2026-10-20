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
        /// <summary>Adds <paramref name="beat"/> at <paramref name="at"/> seconds (&gt;= 0; NaN or negative throws).</summary>
        public void Add(float at, TBeat beat) => throw new NotImplementedException();

        public float Time => throw new NotImplementedException();

        /// <summary>The time of the last beat.</summary>
        public float End => throw new NotImplementedException();

        /// <summary>True when no beat is left to fire.</summary>
        public bool IsDone => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        /// <summary>The beats this step fired, in order; the list is reused by the next call.</summary>
        public IReadOnlyList<TBeat> Advance(float deltaTime) => throw new NotImplementedException();

        /// <summary>Fires every remaining beat (anim.skip); <see cref="Time"/> becomes <see cref="End"/>.</summary>
        public IReadOnlyList<TBeat> Complete() => throw new NotImplementedException();

        /// <summary>Drops every beat; <see cref="Time"/> becomes 0.</summary>
        public void Clear() => throw new NotImplementedException();
    }
}
