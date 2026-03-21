using System;

namespace Unidad.Core.EventBus
{
    /// <summary>
    /// No-op IEventBus for standalone usage outside the DI container.
    /// Zero allocations — Subscribe returns a cached singleton disposable.
    /// </summary>
    public sealed class NullEventBus : IEventBus
    {
        static readonly IDisposable EmptyDisposable = new NullDisposable();

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct => EmptyDisposable;
        public void Publish<T>(T eventData) where T : struct { }
        public void Unsubscribe<T>(Action<T> handler) where T : struct { }
        public void ClearAllSubscriptions() { }

        sealed class NullDisposable : IDisposable { public void Dispose() { } }
    }
}
