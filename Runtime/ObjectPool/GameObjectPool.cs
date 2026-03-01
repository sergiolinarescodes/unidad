using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.ObjectPool
{
    /// <summary>
    /// Generic GameObject pool parameterized on TEntry.
    /// Pool-created GOs use new GameObject() directly (not IGameObjectFactory)
    /// so the factory's DestroyAll() doesn't interfere with pool ownership.
    /// </summary>
    public sealed class GameObjectPool<TEntry> : IDisposable where TEntry : class
    {
        private readonly Queue<TEntry> _available = new();
        private readonly List<TEntry> _all = new();

        private readonly Func<TEntry> _createFunc;
        private readonly Action<TEntry> _resetAction;
        private readonly Action<TEntry> _destroyAction;
        private readonly Func<TEntry, GameObject> _getGameObject;

        private bool _disposed;

        public int TotalCount => _all.Count;
        public int AvailableCount => _available.Count;

        public GameObjectPool(
            Func<TEntry> createFunc,
            Action<TEntry> resetAction,
            Action<TEntry> destroyAction,
            Func<TEntry, GameObject> getGameObject)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _resetAction = resetAction ?? throw new ArgumentNullException(nameof(resetAction));
            _destroyAction = destroyAction ?? throw new ArgumentNullException(nameof(destroyAction));
            _getGameObject = getGameObject ?? throw new ArgumentNullException(nameof(getGameObject));
        }

        /// <summary>Pre-creates entries in inactive state.</summary>
        public void Prewarm(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var entry = _createFunc();
                _all.Add(entry);

                var go = _getGameObject(entry);
                if (go != null) go.SetActive(false);

                _available.Enqueue(entry);
            }
        }

        /// <summary>
        /// Acquires an entry from the pool. Dequeues if available, otherwise creates new.
        /// Skips entries whose GameObject was destroyed externally.
        /// </summary>
        public TEntry Acquire()
        {
            while (_available.Count > 0)
            {
                var entry = _available.Dequeue();
                var go = _getGameObject(entry);

                if (go == null)
                {
                    _all.Remove(entry);
                    continue;
                }

                go.SetActive(true);
                return entry;
            }

            Debug.LogWarning($"[GameObjectPool<{typeof(TEntry).Name}>] Pool exhausted, creating new instance.");
            var newEntry = _createFunc();
            _all.Add(newEntry);
            return newEntry;
        }

        /// <summary>Returns an entry to the pool after calling the reset action.</summary>
        public void Return(TEntry entry)
        {
            if (entry == null) return;

            var go = _getGameObject(entry);
            if (go == null)
            {
                _all.Remove(entry);
                return;
            }

            _resetAction(entry);
            go.SetActive(false);
            _available.Enqueue(entry);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var entry in _all)
                _destroyAction(entry);

            _all.Clear();
            _available.Clear();
        }
    }
}
