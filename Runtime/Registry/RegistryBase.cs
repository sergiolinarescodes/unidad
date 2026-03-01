using System;
using System.Collections.Generic;

namespace Unidad.Core.Registry
{
    /// <summary>
    /// Base implementation of a generic registry.
    /// Provides standard CRUD operations. Not thread-safe.
    /// </summary>
    public class RegistryBase<TKey, TValue> : IRegistry<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _items = new();

        public void Register(TKey key, TValue value)
        {
            if (_items.ContainsKey(key))
                throw new InvalidOperationException($"Key '{key}' is already registered.");
            _items[key] = value;
        }

        public bool TryGet(TKey key, out TValue value) => _items.TryGetValue(key, out value);

        public TValue Get(TKey key)
        {
            if (!_items.TryGetValue(key, out var value))
                throw new KeyNotFoundException($"Key '{key}' not found in registry.");
            return value;
        }

        public bool Has(TKey key) => _items.ContainsKey(key);

        public void Remove(TKey key) => _items.Remove(key);

        public IEnumerable<TKey> Keys => _items.Keys;
        public IEnumerable<TValue> Values => _items.Values;
        public int Count => _items.Count;

        public void Clear() => _items.Clear();
    }
}
