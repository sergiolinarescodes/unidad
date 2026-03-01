using System;
using Unidad.Core.EventBus;

namespace Unidad.Core.HistoryService
{
    /// <summary>
    /// Decorator that wraps IEventBus to record all published events to history.
    /// All methods delegate to the inner event bus, with Publish additionally recording.
    /// </summary>
    internal sealed class RecordingEventBus : IEventBus
    {
        private readonly IEventBus _inner;
        private readonly HistoryService _history;

        public RecordingEventBus(IEventBus inner, HistoryService history)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _history = history ?? throw new ArgumentNullException(nameof(history));
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
            => _inner.Subscribe(handler);

        public void Unsubscribe<T>(Action<T> handler) where T : struct
            => _inner.Unsubscribe(handler);

        public void Publish<T>(T eventData) where T : struct
        {
            _history.RecordEvent(eventData);
            _inner.Publish(eventData);
        }

        public void ClearAllSubscriptions()
            => _inner.ClearAllSubscriptions();
    }
}
