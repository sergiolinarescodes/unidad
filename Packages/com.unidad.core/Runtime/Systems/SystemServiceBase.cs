using System;
using Unidad.Core.EventBus;

namespace Unidad.Core.Systems
{
    /// <summary>
    /// Base class for system services with lifecycle management and auto-unsubscribe.
    /// Services that subscribe to events should extend this and add subscriptions
    /// via the Subscriptions composite disposable.
    /// </summary>
    public abstract class SystemServiceBase : IDisposable
    {
        protected IEventBus EventBus { get; }
        protected CompositeDisposable Subscriptions { get; } = new();

        private bool _disposed;

        protected SystemServiceBase(IEventBus eventBus)
        {
            EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// Subscribe to an event and auto-track the subscription for disposal.
        /// </summary>
        protected void Subscribe<T>(Action<T> handler) where T : struct
        {
            Subscriptions.Add(EventBus.Subscribe(handler));
        }

        /// <summary>
        /// Publish an event through the event bus.
        /// </summary>
        protected void Publish<T>(T eventData) where T : struct
        {
            EventBus.Publish(eventData);
        }

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Subscriptions.Dispose();
        }
    }
}
