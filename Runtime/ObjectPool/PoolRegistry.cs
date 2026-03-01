using System;
using System.Collections.Generic;

namespace Unidad.Core.ObjectPool
{
    /// <summary>
    /// Centralized registry for all object pools.
    /// Enables bulk operations (dispose all, stats) and prevents duplicate pools.
    /// </summary>
    public sealed class PoolRegistry : IDisposable
    {
        private readonly Dictionary<string, IDisposable> _pools = new();

        /// <summary>Register a pool with a unique key.</summary>
        public void Register<TEntry>(string key, GameObjectPool<TEntry> pool) where TEntry : class
        {
            if (_pools.ContainsKey(key))
                throw new InvalidOperationException($"Pool with key '{key}' is already registered.");
            _pools[key] = pool;
        }

        /// <summary>Get a registered pool by key.</summary>
        public GameObjectPool<TEntry> Get<TEntry>(string key) where TEntry : class
        {
            if (!_pools.TryGetValue(key, out var pool))
                throw new KeyNotFoundException($"No pool registered with key '{key}'.");
            return (GameObjectPool<TEntry>)pool;
        }

        /// <summary>Check if a pool is registered.</summary>
        public bool Has(string key) => _pools.ContainsKey(key);

        /// <summary>Get all registered pool keys.</summary>
        public IEnumerable<string> Keys => _pools.Keys;

        /// <summary>Dispose all registered pools.</summary>
        public void Dispose()
        {
            foreach (var pool in _pools.Values)
                pool.Dispose();
            _pools.Clear();
        }
    }
}
