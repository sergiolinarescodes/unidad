using System;

namespace Unidad.Core.EventBus
{
    /// <summary>
    /// Central event bus for decoupled communication between systems.
    /// All game events flow through this service.
    /// Events must be structs (value types) for zero-allocation and copy semantics.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Subscribes a handler to events of type T.
        /// Returns a subscription token that must be disposed to unsubscribe.
        /// </summary>
        IDisposable Subscribe<T>(Action<T> handler) where T : struct;

        /// <summary>
        /// Publishes an event to all subscribers of type T.
        /// </summary>
        void Publish<T>(T eventData) where T : struct;

        /// <summary>
        /// Unsubscribes a specific handler from events of type T.
        /// </summary>
        void Unsubscribe<T>(Action<T> handler) where T : struct;

        /// <summary>
        /// Clears all subscriptions. Used for cleanup between scenarios.
        /// </summary>
        void ClearAllSubscriptions();
    }
}
