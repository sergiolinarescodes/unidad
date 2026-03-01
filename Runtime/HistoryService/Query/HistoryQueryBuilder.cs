using System;
using System.Collections.Generic;
using System.Linq;
using Unidad.Core.HistoryService.Data;
using UnityEngine;

namespace Unidad.Core.HistoryService.Query
{
    /// <summary>
    /// Implementation of fluent query builder for history queries.
    /// </summary>
    internal sealed class HistoryQueryBuilder : IHistoryQueryBuilder
    {
        private readonly IReadOnlyList<HistoryEntry> _source;
        private readonly List<Func<HistoryEntry, bool>> _filters = new();
        private int? _take;
        private int? _skip;

        internal HistoryQueryBuilder(IReadOnlyList<HistoryEntry> source)
        {
            _source = source;
        }

        public IHistoryQueryBuilder OfType<T>() where T : struct
        {
            _filters.Add(e => e.Is<T>());
            return this;
        }

        public IHistoryQueryBuilder ForEntity(string entityId)
        {
            _filters.Add(e =>
                e.PrimaryEntityId == entityId ||
                (e.RelatedEntityIds != null && e.RelatedEntityIds.Contains(entityId)));
            return this;
        }

        public IHistoryQueryBuilder ForPosition(Vector2Int position)
        {
            _filters.Add(e => e.Position.HasValue && e.Position.Value == position);
            return this;
        }

        public IHistoryQueryBuilder InArea(Vector2Int corner1, Vector2Int corner2)
        {
            var minX = Math.Min(corner1.x, corner2.x);
            var maxX = Math.Max(corner1.x, corner2.x);
            var minY = Math.Min(corner1.y, corner2.y);
            var maxY = Math.Max(corner1.y, corner2.y);

            _filters.Add(e =>
                e.Position.HasValue &&
                e.Position.Value.x >= minX && e.Position.Value.x <= maxX &&
                e.Position.Value.y >= minY && e.Position.Value.y <= maxY);
            return this;
        }

        public IHistoryQueryBuilder InTickRange(int fromTick, int toTick)
        {
            _filters.Add(e => e.Timestamp.Tick >= fromTick && e.Timestamp.Tick <= toTick);
            return this;
        }

        public IHistoryQueryBuilder AtTick(int tick)
        {
            _filters.Add(e => e.Timestamp.Tick == tick);
            return this;
        }

        public IHistoryQueryBuilder After(HistoryTimestamp timestamp)
        {
            _filters.Add(e => e.Timestamp > timestamp);
            return this;
        }

        public IHistoryQueryBuilder Before(HistoryTimestamp timestamp)
        {
            _filters.Add(e => e.Timestamp < timestamp);
            return this;
        }

        public IHistoryQueryBuilder CausedBy(long entryId)
        {
            _filters.Add(e => e.CausedByEntryId == entryId);
            return this;
        }

        public IHistoryQueryBuilder Where(Func<HistoryEntry, bool> predicate)
        {
            _filters.Add(predicate);
            return this;
        }

        public IHistoryQueryBuilder Take(int count)
        {
            _take = count;
            return this;
        }

        public IHistoryQueryBuilder Skip(int count)
        {
            _skip = count;
            return this;
        }

        public HistoryQuery Execute()
        {
            IEnumerable<HistoryEntry> result = _source;

            foreach (var filter in _filters)
            {
                result = result.Where(filter);
            }

            if (_skip.HasValue)
                result = result.Skip(_skip.Value);

            if (_take.HasValue)
                result = result.Take(_take.Value);

            return new HistoryQuery(result.ToList());
        }
    }
}
