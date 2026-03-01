using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unidad.Core.HistoryService.Data;
using Unidad.Core.HistoryService.Query;
using UnityEngine;

namespace Unidad.Core.HistoryService
{
    /// <summary>
    /// Generalized implementation of history service for recording and querying event history.
    /// Thread-safe. No game-specific extractors — games register their own via RegisterExtractor.
    /// </summary>
    internal sealed class HistoryService : IHistoryService
    {
        private readonly List<HistoryEntry> _entries = new();
        private readonly object _lock = new();
        private long _nextSequenceId;
        private int _currentTick;
        private int _currentSubTick;
        private bool _isRecording;

        /// <summary>
        /// Maximum number of entries to keep. 0 = unlimited.
        /// When exceeded, oldest entries are dropped.
        /// </summary>
        public int MaxEntries { get; set; }

        private readonly AsyncLocal<long?> _currentCauseId = new();

        private readonly Dictionary<Type, Func<object, string>> _primaryEntityExtractors = new();
        private readonly Dictionary<Type, Func<object, IReadOnlyList<string>>> _relatedEntityExtractors = new();
        private readonly Dictionary<Type, Func<object, Vector2Int?>> _positionExtractors = new();

        #region Recording Control

        public bool IsRecording => _isRecording;

        public void StartRecording()
        {
            lock (_lock) { _isRecording = true; }
        }

        public void StopRecording()
        {
            lock (_lock) { _isRecording = false; }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                _nextSequenceId = 0;
                _currentTick = 0;
                _currentSubTick = 0;
            }
        }

        #endregion

        #region Tick Management

        public int CurrentTick
        {
            get { lock (_lock) { return _currentTick; } }
        }

        public void AdvanceTick()
        {
            lock (_lock)
            {
                _currentTick++;
                _currentSubTick = 0;
            }
        }

        public void SetTick(int tick)
        {
            lock (_lock)
            {
                _currentTick = tick;
                _currentSubTick = 0;
            }
        }

        #endregion

        #region Recording

        /// <summary>
        /// Records an event to history. Called by RecordingEventBus decorator.
        /// </summary>
        internal void RecordEvent<T>(T eventData) where T : struct
        {
            lock (_lock)
            {
                if (!_isRecording) return;

                var entry = new HistoryEntry
                {
                    SequenceId = _nextSequenceId++,
                    Timestamp = new HistoryTimestamp(_currentTick, _currentSubTick++),
                    EventTypeName = typeof(T).Name,
                    EventData = eventData,
                    CausedByEntryId = _currentCauseId.Value,
                    PrimaryEntityId = ExtractPrimaryEntity(eventData),
                    RelatedEntityIds = ExtractRelatedEntities(eventData),
                    Position = ExtractPosition(eventData)
                };
                _entries.Add(entry);

                if (MaxEntries > 0 && _entries.Count > MaxEntries)
                {
                    _entries.RemoveRange(0, _entries.Count - MaxEntries);
                }
            }
        }

        #endregion

        #region Query API

        public int EntryCount
        {
            get { lock (_lock) { return _entries.Count; } }
        }

        public HistoryQuery GetAll()
        {
            lock (_lock) { return new HistoryQuery(_entries.ToList()); }
        }

        public HistoryQuery Where(Func<HistoryEntry, bool> predicate)
        {
            lock (_lock) { return new HistoryQuery(_entries.Where(predicate).ToList()); }
        }

        public HistoryQuery OfType<T>() where T : struct
        {
            lock (_lock) { return new HistoryQuery(_entries.Where(e => e.Is<T>()).ToList()); }
        }

        public HistoryQuery ForEntity(string entityId)
        {
            lock (_lock)
            {
                return new HistoryQuery(_entries.Where(e =>
                    e.PrimaryEntityId == entityId ||
                    (e.RelatedEntityIds != null && e.RelatedEntityIds.Contains(entityId))).ToList());
            }
        }

        public HistoryQuery ForPosition(Vector2Int position)
        {
            lock (_lock)
            {
                return new HistoryQuery(_entries.Where(e =>
                    e.Position.HasValue && e.Position.Value == position).ToList());
            }
        }

        public HistoryQuery InArea(Vector2Int corner1, Vector2Int corner2)
        {
            var minX = Math.Min(corner1.x, corner2.x);
            var maxX = Math.Max(corner1.x, corner2.x);
            var minY = Math.Min(corner1.y, corner2.y);
            var maxY = Math.Max(corner1.y, corner2.y);

            lock (_lock)
            {
                return new HistoryQuery(_entries.Where(e =>
                    e.Position.HasValue &&
                    e.Position.Value.x >= minX && e.Position.Value.x <= maxX &&
                    e.Position.Value.y >= minY && e.Position.Value.y <= maxY).ToList());
            }
        }

        public HistoryQuery InTickRange(int fromTick, int toTick)
        {
            lock (_lock)
            {
                return new HistoryQuery(_entries.Where(e =>
                    e.Timestamp.Tick >= fromTick && e.Timestamp.Tick <= toTick).ToList());
            }
        }

        public HistoryQuery AtTick(int tick)
        {
            lock (_lock)
            {
                return new HistoryQuery(_entries.Where(e => e.Timestamp.Tick == tick).ToList());
            }
        }

        public IHistoryQueryBuilder Query()
        {
            lock (_lock) { return new HistoryQueryBuilder(_entries.ToList()); }
        }

        #endregion

        #region Causality

        public HistoryQuery GetCausedBy(long entryId)
        {
            lock (_lock)
            {
                return new HistoryQuery(_entries.Where(e => e.CausedByEntryId == entryId).ToList());
            }
        }

        public HistoryQuery GetCausalChain(long entryId)
        {
            lock (_lock)
            {
                var chain = new List<HistoryEntry>();
                var current = _entries.FirstOrDefault(e => e.SequenceId == entryId);

                while (current != null && current.CausedByEntryId.HasValue)
                {
                    var causeId = current.CausedByEntryId.Value;
                    current = _entries.FirstOrDefault(e => e.SequenceId == causeId);
                    if (current != null)
                        chain.Insert(0, current);
                }

                return new HistoryQuery(chain);
            }
        }

        public IDisposable WithCause(long causeEntryId)
        {
            var previousValue = _currentCauseId.Value;
            _currentCauseId.Value = causeEntryId;
            return new CausalityScope(this, previousValue);
        }

        private sealed class CausalityScope : IDisposable
        {
            private readonly HistoryService _service;
            private readonly long? _previousValue;

            public CausalityScope(HistoryService service, long? previousValue)
            {
                _service = service;
                _previousValue = previousValue;
            }

            public void Dispose()
            {
                _service._currentCauseId.Value = _previousValue;
            }
        }

        #endregion

        #region Statistics

        public IReadOnlyDictionary<string, int> GetEventTypeCounts()
        {
            lock (_lock)
            {
                return _entries
                    .GroupBy(e => e.EventTypeName)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
        }

        #endregion

        #region Extractor Registration

        public void RegisterExtractor<T>(
            Func<T, string> primaryEntityExtractor = null,
            Func<T, Vector2Int?> positionExtractor = null,
            Func<T, IReadOnlyList<string>> relatedEntitiesExtractor = null
        ) where T : struct
        {
            if (primaryEntityExtractor != null)
                _primaryEntityExtractors[typeof(T)] = obj => primaryEntityExtractor((T)obj);
            if (positionExtractor != null)
                _positionExtractors[typeof(T)] = obj => positionExtractor((T)obj);
            if (relatedEntitiesExtractor != null)
                _relatedEntityExtractors[typeof(T)] = obj => relatedEntitiesExtractor((T)obj);
        }

        private string ExtractPrimaryEntity<T>(T eventData) where T : struct
        {
            return _primaryEntityExtractors.TryGetValue(typeof(T), out var extractor)
                ? extractor(eventData)
                : null;
        }

        private IReadOnlyList<string> ExtractRelatedEntities<T>(T eventData) where T : struct
        {
            return _relatedEntityExtractors.TryGetValue(typeof(T), out var extractor)
                ? extractor(eventData)
                : null;
        }

        private Vector2Int? ExtractPosition<T>(T eventData) where T : struct
        {
            return _positionExtractors.TryGetValue(typeof(T), out var extractor)
                ? extractor(eventData)
                : null;
        }

        #endregion
    }
}
