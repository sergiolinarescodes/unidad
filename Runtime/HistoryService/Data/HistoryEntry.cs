using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.HistoryService.Data
{
    /// <summary>
    /// Wraps an event with metadata for history tracking.
    /// Provides indexing by entity ID and position for efficient querying.
    /// Uses Vector2Int for position (framework-generic, no game-specific GridPosition).
    /// </summary>
    public sealed record HistoryEntry
    {
        /// <summary>Unique sequential ID for this entry.</summary>
        public long SequenceId { get; init; }

        /// <summary>When this event occurred (tick + sub-tick).</summary>
        public HistoryTimestamp Timestamp { get; init; }

        /// <summary>The event type name (e.g., "UnitSpawnedEvent").</summary>
        public string EventTypeName { get; init; }

        /// <summary>The actual event data (boxed struct).</summary>
        public object EventData { get; init; }

        /// <summary>Optional: ID of the entry that caused this event.</summary>
        public long? CausedByEntryId { get; init; }

        /// <summary>Optional: Primary entity ID involved (for filtering).</summary>
        public string PrimaryEntityId { get; init; }

        /// <summary>Optional: Secondary entity IDs involved.</summary>
        public IReadOnlyList<string> RelatedEntityIds { get; init; }

        /// <summary>Optional: Grid position associated with the event.</summary>
        public Vector2Int? Position { get; init; }

        /// <summary>Helper to get strongly-typed event data.</summary>
        public T GetEvent<T>() where T : struct => (T)EventData;

        /// <summary>Check if this entry contains an event of type T.</summary>
        public bool Is<T>() where T : struct => EventData is T;

        public override string ToString()
            => $"{Timestamp} {EventTypeName}" +
               (PrimaryEntityId != null ? $" [{PrimaryEntityId}]" : "") +
               (Position.HasValue ? $" @{Position.Value}" : "");
    }
}
