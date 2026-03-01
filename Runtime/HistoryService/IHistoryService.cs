using System;
using System.Collections.Generic;
using Unidad.Core.HistoryService.Data;
using Unidad.Core.HistoryService.Query;
using UnityEngine;

namespace Unidad.Core.HistoryService
{
    /// <summary>
    /// Service for recording and querying event history.
    /// Used for debugging, testing, replay functionality, and verifying emergent behavior.
    /// Generalized: uses Vector2Int for position instead of game-specific types.
    /// </summary>
    public interface IHistoryService
    {
        #region Recording Control

        bool IsRecording { get; }
        void StartRecording();
        void StopRecording();
        void Clear();

        #endregion

        #region Tick Management

        int CurrentTick { get; }
        void AdvanceTick();
        void SetTick(int tick);

        #endregion

        #region Query API

        HistoryQuery GetAll();
        HistoryQuery Where(Func<HistoryEntry, bool> predicate);
        HistoryQuery OfType<T>() where T : struct;
        HistoryQuery ForEntity(string entityId);
        HistoryQuery ForPosition(Vector2Int position);
        HistoryQuery InArea(Vector2Int corner1, Vector2Int corner2);
        HistoryQuery InTickRange(int fromTick, int toTick);
        HistoryQuery AtTick(int tick);
        IHistoryQueryBuilder Query();

        #endregion

        #region Causality

        HistoryQuery GetCausedBy(long entryId);
        HistoryQuery GetCausalChain(long entryId);
        IDisposable WithCause(long causeEntryId);

        #endregion

        #region Statistics

        int EntryCount { get; }
        IReadOnlyDictionary<string, int> GetEventTypeCounts();

        #endregion

        #region Entity/Position Extraction Registration

        /// <summary>
        /// Register extractors for a custom event type.
        /// Games call this to tell the history service how to extract entity/position from their events.
        /// </summary>
        void RegisterExtractor<T>(
            Func<T, string> primaryEntityExtractor = null,
            Func<T, Vector2Int?> positionExtractor = null,
            Func<T, IReadOnlyList<string>> relatedEntitiesExtractor = null
        ) where T : struct;

        #endregion
    }
}
