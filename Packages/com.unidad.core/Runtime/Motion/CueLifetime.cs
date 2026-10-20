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
        // _items[i] belongs to _entries[i]; both in add order.
        private readonly List<(int Id, float Remaining, bool Held)> _entries = new();
        private readonly List<T> _items = new();
        private readonly List<T> _expired = new();
        private int _lastId;

        /// <summary>Adds <paramref name="item"/> for <paramref name="ttl"/> seconds (&gt; 0, or infinity for sticky).</summary>
        public CueHandle Add(T item, float ttl)
        {
            if (!(ttl > 0f)) throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "A cue lives > 0 seconds.");
            var handle = new CueHandle(++_lastId);
            _entries.Add((handle.Value, ttl, false));
            _items.Add(item);
            Version++;
            return handle;
        }

        public bool Hold(CueHandle handle, bool held)
        {
            int i = IndexOf(handle);
            if (i < 0) return false;
            if (_entries[i].Held != held)
            {
                _entries[i] = (_entries[i].Id, _entries[i].Remaining, held);
                Version++;
            }
            return true;
        }

        public bool Remove(CueHandle handle)
        {
            int i = IndexOf(handle);
            if (i < 0) return false;
            _entries.RemoveAt(i);
            _items.RemoveAt(i);
            Version++;
            return true;
        }

        /// <summary>Seconds left; 0 for an unknown handle.</summary>
        public float Remaining(CueHandle handle)
        {
            int i = IndexOf(handle);
            return i < 0 ? 0f : _entries[i].Remaining;
        }

        /// <summary>The items that expired this call, in add order; the list is reused by the next call.</summary>
        public IReadOnlyList<T> Advance(float deltaTime)
        {
            _expired.Clear();
            int kept = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!entry.Held) entry.Remaining -= deltaTime;
                if (entry.Remaining <= 0f)
                {
                    _expired.Add(_items[i]);
                    continue;
                }
                _entries[kept] = entry;
                _items[kept] = _items[i];
                kept++;
            }
            if (_expired.Count > 0)
            {
                _entries.RemoveRange(kept, _entries.Count - kept);
                _items.RemoveRange(kept, _items.Count - kept);
                Version++;
            }
            return _expired;
        }

        /// <summary>Live items, in add order.</summary>
        public IReadOnlyList<T> Current => _items;

        public int Version { get; private set; }

        public int Count => _entries.Count;

        private int IndexOf(CueHandle handle)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Id == handle.Value) return i;
            return -1;
        }
    }
}
