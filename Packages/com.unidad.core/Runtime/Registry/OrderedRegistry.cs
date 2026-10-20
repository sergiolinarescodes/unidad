using System;
using System.Collections.Generic;

namespace Unidad.Core.Registry
{
    /// <summary>
    /// An INSERTION-ORDERED registry of values keyed by a string id, exposing a live
    /// <see cref="IReadOnlyList{T}"/> read model. Reach for this instead of hand-rolling the
    /// triple (<c>List&lt;string&gt; _order</c> + <c>Dictionary&lt;string, T&gt; _byId</c> +
    /// <c>List&lt;T&gt; _view</c> + a manual <c>RebuildView()</c>): that shape has one silent
    /// failure mode, a mutation that forgets its rebuild, after which any read model mixing the
    /// two is wrong with no exception. Here every mutator keeps <see cref="Values"/> in step, so
    /// there is nothing a caller can forget.
    /// <para>
    /// <see cref="Values"/> and <see cref="Ids"/> are LIVE views — the same object every access,
    /// always current. Mutating while iterating requires a snapshot first
    /// (<see cref="IdSnapshot"/>). Not thread-safe.
    /// </para>
    /// </summary>
    public sealed class OrderedRegistry<TValue>
    {
        private readonly List<string> _ids = new();
        private readonly List<TValue> _values = new();
        private readonly Dictionary<string, int> _indexById = new();

        /// <summary>Number of rows.</summary>
        public int Count => _ids.Count;

        /// <summary>Ids in insertion order. Live — reflects mutations immediately.</summary>
        public IReadOnlyList<string> Ids => _ids;

        /// <summary>
        /// Values in insertion order. Live, and never needs a rebuild call. Safe to hand straight
        /// out as a service's read model.
        /// </summary>
        public IReadOnlyList<TValue> Values => _values;

        /// <summary>True if <paramref name="id"/> is present. A null id is simply absent.</summary>
        public bool Contains(string id) => id != null && _indexById.ContainsKey(id);

        /// <summary>
        /// Look up by id. A null id is treated as absent (returns false) rather than throwing, so
        /// callers need no <c>?? string.Empty</c> guard.
        /// </summary>
        public bool TryGet(string id, out TValue value)
        {
            if (id != null && _indexById.TryGetValue(id, out var index))
            {
                value = _values[index];
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Look up by id, throwing <see cref="KeyNotFoundException"/> if absent.</summary>
        public TValue Get(string id)
        {
            if (!TryGet(id, out var value))
                throw new KeyNotFoundException($"Id '{id}' not found in registry.");
            return value;
        }

        /// <summary>The value at an insertion-order position.</summary>
        public TValue this[int index] => _values[index];

        /// <summary>
        /// Append a NEW row. Throws <see cref="InvalidOperationException"/> on a duplicate id —
        /// callers that treat a duplicate as a refusal should guard with <see cref="Contains"/>
        /// or use <see cref="TryAdd"/>, so the refusal event still fires.
        /// </summary>
        public void Add(string id, TValue value)
        {
            if (!TryAdd(id, value))
                throw new InvalidOperationException($"Id '{id}' is already registered.");
        }

        /// <summary>Append a new row; false (and no change) if the id is already present.</summary>
        public bool TryAdd(string id, TValue value)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (_indexById.ContainsKey(id)) return false;

            _indexById[id] = _ids.Count;
            _ids.Add(id);
            _values.Add(value);
            return true;
        }

        /// <summary>
        /// Replace the value stored under an EXISTING id, keeping its position. Throws
        /// <see cref="KeyNotFoundException"/> if absent — a replace that silently appended would
        /// put the row out of order, which is the drift this type exists to stop.
        /// </summary>
        public void Replace(string id, TValue value)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (!_indexById.TryGetValue(id, out var index))
                throw new KeyNotFoundException($"Id '{id}' not found in registry.");
            _values[index] = value;
        }

        /// <summary>
        /// Remove a row, preserving the relative order of the survivors. False if absent
        /// (including a null id).
        /// </summary>
        public bool Remove(string id)
        {
            if (id == null || !_indexById.TryGetValue(id, out var index)) return false;

            _ids.RemoveAt(index);
            _values.RemoveAt(index);
            _indexById.Remove(id);
            for (var i = index; i < _ids.Count; i++) _indexById[_ids[i]] = i;
            return true;
        }

        /// <summary>Drop every row.</summary>
        public void Clear()
        {
            _ids.Clear();
            _values.Clear();
            _indexById.Clear();
        }

        /// <summary>
        /// A defensive copy of the ids, for the "mutate while iterating" case — publishing an
        /// event mid-loop can let a subscriber add or remove rows, invalidating a live
        /// enumeration of <see cref="Ids"/>.
        /// </summary>
        public List<string> IdSnapshot() => new(_ids);
    }
}
