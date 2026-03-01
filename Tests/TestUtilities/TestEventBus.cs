using System;
using System.Collections.Generic;
using System.Linq;
using Unidad.Core.EventBus;
using Unidad.Core.HistoryService;
using Unidad.Core.HistoryService.Data;
using UnityEngine;

namespace Unidad.Core.Tests.Tests.TestUtilities
{
    /// <summary>
    /// Test-friendly event bus with built-in history recording.
    /// Backward compatible with MockEventBus API while adding history capabilities.
    /// </summary>
    public sealed class TestEventBus : IEventBus
    {
        private readonly HistoryService.HistoryService _history;
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public TestEventBus()
        {
            _history = new HistoryService.HistoryService();
            _history.StartRecording();
        }

        /// <summary>Access to history service for advanced queries.</summary>
        public IHistoryService History => _history;

        #region Backward Compatible MockEventBus API

        public IReadOnlyList<object> PublishedEvents =>
            _history.GetAll().Select(e => e.EventData).ToList();

        public T GetPublishedEvent<T>(int index = 0) where T : struct
        {
            var matching = _history.OfType<T>().ToList();
            if (index >= matching.Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Only {matching.Count} events of type {typeof(T).Name} were published");
            return matching[index].GetEvent<T>();
        }

        public int CountEventsOfType<T>() where T : struct
            => _history.OfType<T>().Count;

        public bool HasEventOfType<T>() where T : struct
            => _history.OfType<T>().Any();

        public void Reset()
        {
            _history.Clear();
            _history.SetTick(0);
        }

        #endregion

        #region IEventBus Implementation

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type))
                _handlers[type] = new List<Delegate>();
            _handlers[type].Add(handler);

            return new ActionDisposable(() =>
            {
                if (_handlers.TryGetValue(type, out var list))
                    list.Remove(handler);
            });
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public void Publish<T>(T eventData) where T : struct
        {
            _history.RecordEvent(eventData);

            if (_handlers.TryGetValue(typeof(T), out var handlerList))
            {
                var handlers = new List<Delegate>(handlerList);
                foreach (var handler in handlers)
                    ((Action<T>)handler)(eventData);
            }
        }

        public void ClearAllSubscriptions() => _handlers.Clear();

        #endregion

        #region History-Based Assertions

        /// <summary>Start building a sequence assertion.</summary>
        public SequenceAssertion ExpectSequence() => new(_history);

        /// <summary>Advance to the next tick.</summary>
        public void AdvanceTick() => _history.AdvanceTick();

        /// <summary>Set current tick.</summary>
        public void SetTick(int tick) => _history.SetTick(tick);

        /// <summary>Get entries for an entity.</summary>
        public HistoryQuery ForEntity(string entityId) => _history.ForEntity(entityId);

        /// <summary>Get entries at a position.</summary>
        public HistoryQuery ForPosition(Vector2Int position) => _history.ForPosition(position);

        /// <summary>Get entries at a specific tick.</summary>
        public HistoryQuery AtTick(int tick) => _history.AtTick(tick);

        /// <summary>Register extractors for a custom event type.</summary>
        public void RegisterExtractor<T>(
            Func<T, string> primaryEntityExtractor = null,
            Func<T, Vector2Int?> positionExtractor = null,
            Func<T, IReadOnlyList<string>> relatedEntitiesExtractor = null
        ) where T : struct
        {
            _history.RegisterExtractor(primaryEntityExtractor, positionExtractor, relatedEntitiesExtractor);
        }

        #endregion
    }
}
