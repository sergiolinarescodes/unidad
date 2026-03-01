using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;

namespace Unidad.Core.Tests.Tests.TestUtilities
{
    /// <summary>
    /// Lightweight mock event bus for unit tests.
    /// Records published events and dispatches to subscribers.
    /// For tests that need history queries, use <see cref="TestEventBus"/> instead.
    /// </summary>
    public sealed class MockEventBus : IEventBus
    {
        private readonly List<object> _publishedEvents = new();
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public IReadOnlyList<object> PublishedEvents => _publishedEvents;

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
            _publishedEvents.Add(eventData);

            if (_handlers.TryGetValue(typeof(T), out var handlerList))
            {
                var handlers = new List<Delegate>(handlerList);
                foreach (var handler in handlers)
                    ((Action<T>)handler)(eventData);
            }
        }

        public void ClearAllSubscriptions()
        {
            _handlers.Clear();
        }

        public T GetPublishedEvent<T>(int index = 0) where T : struct
        {
            var matching = new List<T>();
            foreach (var evt in _publishedEvents)
            {
                if (evt is T typedEvent)
                    matching.Add(typedEvent);
            }

            if (index >= matching.Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Only {matching.Count} events of type {typeof(T).Name} were published");

            return matching[index];
        }

        public int CountEventsOfType<T>() where T : struct
        {
            var count = 0;
            foreach (var evt in _publishedEvents)
            {
                if (evt is T) count++;
            }
            return count;
        }

        public bool HasEventOfType<T>() where T : struct
            => CountEventsOfType<T>() > 0;

        public IReadOnlyList<T> GetEventsOfType<T>() where T : struct
        {
            var result = new List<T>();
            foreach (var evt in _publishedEvents)
            {
                if (evt is T typedEvent)
                    result.Add(typedEvent);
            }
            return result;
        }

        public void ClearEvents() => _publishedEvents.Clear();
        public void Reset() => _publishedEvents.Clear();
    }
}
