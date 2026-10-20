using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;

namespace Unidad.Core.Motion
{
    /// <summary>One entry of a <see cref="CueLifetime{T}"/>.</summary>
    public readonly record struct CueHandle(int Value)
    {
        public static readonly CueHandle None = default;
    }

    /// <summary>
    /// Cues (toasts, popups) that outlive the beat that showed them, each for its own time to live. Held entries do not age
    /// (hover holds a toast). <see cref="IReadModel{T}.Version"/> changes on add, remove, hold and expiry.
    /// </summary>
    public sealed class CueLifetime<T> : IReadModel<IReadOnlyList<T>>
    {
        /// <summary>Adds <paramref name="item"/> for <paramref name="ttl"/> seconds (&gt; 0, or infinity for sticky).</summary>
        public CueHandle Add(T item, float ttl) => throw new NotImplementedException();

        public bool Hold(CueHandle handle, bool held) => throw new NotImplementedException();

        public bool Remove(CueHandle handle) => throw new NotImplementedException();

        /// <summary>Seconds left; 0 for an unknown handle.</summary>
        public float Remaining(CueHandle handle) => throw new NotImplementedException();

        /// <summary>The items that expired this call, in add order; the list is reused by the next call.</summary>
        public IReadOnlyList<T> Advance(float deltaTime) => throw new NotImplementedException();

        /// <summary>Live items, in add order.</summary>
        public IReadOnlyList<T> Current => throw new NotImplementedException();

        public int Version => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();
    }
}
