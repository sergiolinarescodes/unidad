using System;
using System.Collections.Generic;

namespace Unidad.Core.EventBus
{
    /// <summary>
    /// Thread-safe event bus implementation.
    /// Uses lock-based synchronization for safe concurrent access.
    /// </summary>
    internal sealed class EventBus : IEventBus, IDisposable
    {
        private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();
        private readonly object _lock = new();

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(T);
            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(eventType, out var handlers))
                {
                    handlers = new List<Delegate>();
                    _subscriptions[eventType] = handlers;
                }
                handlers.Add(handler);
            }
            return new ActionDisposable(() => Unsubscribe(handler));
        }

        public void Publish<T>(T eventData) where T : struct
        {
            var eventType = typeof(T);
            List<Delegate> handlersCopy;
            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(eventType, out var handlers))
                    return;
                handlersCopy = new List<Delegate>(handlers);
            }

            foreach (var handler in handlersCopy)
            {
                try
                {
                    ((Action<T>)handler).Invoke(eventData);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var eventType = typeof(T);
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(eventType, out var handlers))
                {
                    handlers.Remove(handler);
                }
            }
        }

        public void ClearAllSubscriptions()
        {
            lock (_lock)
            {
                _subscriptions.Clear();
            }
        }

        public void Dispose()
        {
            ClearAllSubscriptions();
        }
    }
}
