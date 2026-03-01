using System;
using Unidad.Core.HistoryService.Data;
using UnityEngine;

namespace Unidad.Core.HistoryService.Query
{
    /// <summary>
    /// Fluent builder for complex history queries.
    /// Chain methods to build filters, then call Execute() to get results.
    /// </summary>
    public interface IHistoryQueryBuilder
    {
        IHistoryQueryBuilder OfType<T>() where T : struct;
        IHistoryQueryBuilder ForEntity(string entityId);
        IHistoryQueryBuilder ForPosition(Vector2Int position);
        IHistoryQueryBuilder InArea(Vector2Int corner1, Vector2Int corner2);
        IHistoryQueryBuilder InTickRange(int fromTick, int toTick);
        IHistoryQueryBuilder AtTick(int tick);
        IHistoryQueryBuilder After(HistoryTimestamp timestamp);
        IHistoryQueryBuilder Before(HistoryTimestamp timestamp);
        IHistoryQueryBuilder CausedBy(long entryId);
        IHistoryQueryBuilder Where(Func<HistoryEntry, bool> predicate);
        IHistoryQueryBuilder Take(int count);
        IHistoryQueryBuilder Skip(int count);
        HistoryQuery Execute();
    }
}
