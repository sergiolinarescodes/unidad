namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Contract for pooled objects that want reset/init callbacks. Implementing this lets
    /// the adapter's <c>ComponentPool</c> reset state automatically on return and prime it on acquire.
    /// Engine-free by shape, but colocated with the pool cluster since nothing in the pure core
    /// consumes it.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Reset state for reuse. Called when the object is returned to the pool.</summary>
        void OnReturnToPool();

        /// <summary>Initialize state for use. Called when the object is acquired from the pool.</summary>
        void OnAcquireFromPool();
    }
}
