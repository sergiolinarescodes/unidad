namespace Unidad.Core.ObjectPool
{
    /// <summary>
    /// Contract for objects that can be pooled.
    /// Implementing this interface allows automatic reset when returned to pool.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Reset state for reuse. Called when object is returned to pool.</summary>
        void OnReturnToPool();

        /// <summary>Initialize state for use. Called when object is acquired from pool.</summary>
        void OnAcquireFromPool();
    }
}
