using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unidad.Adapter
{
    /// <summary>Reused components under one root: acquire, return, prewarm. A foreign item throws; a double return is ignored.</summary>
    public sealed class ComponentPool<T> : IDisposable where T : Component
    {
        private readonly PoolCore<T> _core;

        public ComponentPool(Func<T> create, Transform root, Action<T> reset = null, Action<T> destroy = null)
        {
            if (create == null) throw new ArgumentNullException(nameof(create));
            if (!root) throw new ArgumentNullException(nameof(root));
            _core = new PoolCore<T>(
                () =>
                {
                    var entry = create();
                    if (entry && !entry.transform.parent) entry.transform.SetParent(root, false);
                    return entry ? entry : null;
                },
                entry => entry,
                (entry, active) => entry.gameObject.SetActive(active),
                entry => entry.transform.SetParent(root, false),
                reset,
                destroy ?? (entry => Object.Destroy(entry.gameObject)),
                $"ComponentPool<{typeof(T).Name}>");
        }

        public int TotalCount => _core.TotalCount;

        public int AvailableCount => _core.AvailableCount;

        public void Prewarm(int count) => _core.Prewarm(count);

        public T Acquire() => _core.Acquire();

        /// <summary>Never allocates: false when nothing is available.</summary>
        public bool TryAcquire(out T entry) => _core.TryAcquire(out entry);

        public void Return(T entry) => _core.Return(entry);

        public void Dispose() => _core.Dispose();
    }

    /// <summary>The scene's pools by key; disposed with the container.</summary>
    public sealed class PoolRegistry : IDisposable
    {
        private readonly Dictionary<string, IDisposable> _pools = new();

        public void Register<T>(string key, ComponentPool<T> pool) where T : Component
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (_pools.ContainsKey(key)) throw new InvalidOperationException($"Pool with key '{key}' is already registered.");
            _pools[key] = pool;
        }

        public ComponentPool<T> Get<T>(string key) where T : Component
        {
            if (!_pools.TryGetValue(key, out var pool)) throw new KeyNotFoundException($"No pool registered with key '{key}'.");
            return (ComponentPool<T>)pool;
        }

        public bool Has(string key) => _pools.ContainsKey(key);

        public IEnumerable<string> Keys => _pools.Keys;

        public void Dispose()
        {
            foreach (var pool in _pools.Values) pool.Dispose();
            _pools.Clear();
        }
    }
}
