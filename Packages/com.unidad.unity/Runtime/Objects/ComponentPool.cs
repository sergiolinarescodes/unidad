using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>Reused components under one root: acquire, return, prewarm. A foreign item throws; a double return is ignored.</summary>
    public sealed class ComponentPool<T> : IDisposable where T : Component
    {
        public ComponentPool(Func<T> create, Transform root, Action<T> reset = null, Action<T> destroy = null) => throw new NotImplementedException();

        public int TotalCount => throw new NotImplementedException();

        public int AvailableCount => throw new NotImplementedException();

        public void Prewarm(int count) => throw new NotImplementedException();

        public T Acquire() => throw new NotImplementedException();

        /// <summary>Never allocates: false when nothing is available.</summary>
        public bool TryAcquire(out T entry) => throw new NotImplementedException();

        public void Return(T entry) => throw new NotImplementedException();

        public void Dispose() => throw new NotImplementedException();
    }

    /// <summary>The scene's pools by key; disposed with the container.</summary>
    public sealed class PoolRegistry : IDisposable
    {
        public void Register<T>(string key, ComponentPool<T> pool) where T : Component => throw new NotImplementedException();

        public ComponentPool<T> Get<T>(string key) where T : Component => throw new NotImplementedException();

        public bool Has(string key) => throw new NotImplementedException();

        public IEnumerable<string> Keys => throw new NotImplementedException();

        public void Dispose() => throw new NotImplementedException();
    }
}
