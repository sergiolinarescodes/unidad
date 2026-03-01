using System.Collections.Generic;

namespace Unidad.Core.Registry
{
    /// <summary>
    /// Generic registry interface for key-value stores.
    /// Used for registering definitions, configurations, or any keyed data.
    /// </summary>
    public interface IRegistry<TKey, TValue>
    {
        void Register(TKey key, TValue value);
        bool TryGet(TKey key, out TValue value);
        TValue Get(TKey key);
        bool Has(TKey key);
        void Remove(TKey key);
        IEnumerable<TKey> Keys { get; }
        IEnumerable<TValue> Values { get; }
        int Count { get; }
        void Clear();
    }
}
