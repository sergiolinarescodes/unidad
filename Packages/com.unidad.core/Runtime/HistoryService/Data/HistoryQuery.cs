using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Unidad.Core.HistoryService.Data
{
    /// <summary>
    /// Result of a history query - an immutable list of matching entries.
    /// Provides convenience methods for common operations.
    /// </summary>
    public sealed class HistoryQuery : IReadOnlyList<HistoryEntry>
    {
        private readonly IReadOnlyList<HistoryEntry> _entries;

        internal HistoryQuery(IReadOnlyList<HistoryEntry> entries)
        {
            _entries = entries ?? new List<HistoryEntry>();
        }

        public HistoryEntry this[int index] => _entries[index];
        public int Count => _entries.Count;

        public IEnumerator<HistoryEntry> GetEnumerator() => _entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Returns true if no entries match the query.</summary>
        public bool IsEmpty => _entries.Count == 0;

        /// <summary>Returns true if at least one entry matches the query.</summary>
        public bool Any() => _entries.Count > 0;

        public HistoryEntry First() => _entries[0];
        public HistoryEntry FirstOrDefault() => _entries.Count > 0 ? _entries[0] : null;
        public HistoryEntry Last() => _entries[_entries.Count - 1];
        public HistoryEntry LastOrDefault() => _entries.Count > 0 ? _entries[_entries.Count - 1] : null;

        /// <summary>Gets the last N entries.</summary>
        public IEnumerable<HistoryEntry> TakeLast(int count)
        {
            var skip = _entries.Count - count;
            return skip > 0 ? _entries.Skip(skip) : _entries;
        }

        /// <summary>Get all events of a specific type.</summary>
        public IEnumerable<T> EventsOfType<T>() where T : struct
            => _entries.Where(e => e.Is<T>()).Select(e => e.GetEvent<T>());

        /// <summary>Get all entries of a specific event type.</summary>
        public IEnumerable<HistoryEntry> EntriesOfType<T>() where T : struct
            => _entries.Where(e => e.Is<T>());

        /// <summary>Count entries of a specific event type.</summary>
        public int CountOfType<T>() where T : struct
            => _entries.Count(e => e.Is<T>());

        /// <summary>Check if any entry is of the specified type.</summary>
        public bool HasType<T>() where T : struct
            => _entries.Any(e => e.Is<T>());

        /// <summary>Creates an empty query result.</summary>
        public static HistoryQuery Empty => new(new List<HistoryEntry>());
    }
}
