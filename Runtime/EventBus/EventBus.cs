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
        // Cached immutable snapshot per event type so Publish allocates nothing on the hot path.
        // Invalidation contract: ANY mutation of a type's handler list (Subscribe/Unsubscribe/Clear)
        // MUST drop that type's snapshot so the next Publish lazily rebuilds it. A Publish already
        // in flight keeps iterating its captured array, so mutating mid-dispatch affects only later
        // publishes — matching the old defensive-copy semantics exactly.
        private readonly Dictionary<Type, Delegate[]> _snapshots = new();
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
                _snapshots.Remove(eventType);
            }
            return new ActionDisposable(() => Unsubscribe(handler));
        }

        public void Publish<T>(T eventData) where T : struct
        {
            var eventType = typeof(T);
            Delegate[] handlers;
            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(eventType, out var registered))
                    return;
                if (!_snapshots.TryGetValue(eventType, out handlers))
                {
                    handlers = registered.ToArray();
                    _snapshots[eventType] = handlers;
                }
            }

            // Iterate the captured reference: a mutation mid-dispatch drops the cache but never
            // touches this array, so the in-flight publish sees the pre-mutation handler set.
            foreach (var handler in handlers)
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
                if (_subscriptions.TryGetValue(eventType, out var handlers)
                    && handlers.Remove(handler))
                {
                    _snapshots.Remove(eventType);
                }
            }
        }

        public void ClearAllSubscriptions()
        {
            lock (_lock)
            {
                _subscriptions.Clear();
                _snapshots.Clear();
            }
        }

        public void Dispose()
        {
            ClearAllSubscriptions();
        }
    }
}
