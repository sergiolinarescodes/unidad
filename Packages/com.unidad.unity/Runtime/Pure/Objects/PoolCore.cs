using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.Diagnostics;

namespace Unidad.Adapter
{
    /// <summary>ComponentPool's bookkeeping over any class; the engine calls (alive check, activation, parking) are passed in.</summary>
    internal sealed class PoolCore<T> : IDisposable where T : class
    {
        private readonly Queue<T> _available = new();
        private readonly List<T> _all = new();
        private readonly HashSet<T> _parked = new();
        private readonly Func<T> _create;
        private readonly Func<T, bool> _isAlive;
        private readonly Action<T, bool> _setActive;
        private readonly Action<T> _park, _reset, _destroy;
        private readonly string _label;
        private bool _disposed;

        public PoolCore(Func<T> create, Func<T, bool> isAlive, Action<T, bool> setActive, Action<T> park, Action<T> reset,
            Action<T> destroy, string label)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _isAlive = isAlive ?? throw new ArgumentNullException(nameof(isAlive));
            _setActive = setActive ?? throw new ArgumentNullException(nameof(setActive));
            _destroy = destroy ?? throw new ArgumentNullException(nameof(destroy));
            _park = park;
            _reset = reset;
            _label = label;
        }

        public int TotalCount => _all.Count;

        public int AvailableCount => _available.Count;

        public void Prewarm(int count)
        {
            ThrowIfDisposed();
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            for (var i = 0; i < count; i++)
            {
                var entry = CreateTracked();
                _setActive(entry, false);
                _available.Enqueue(entry);
                _parked.Add(entry);
            }
        }

        /// <summary>Never allocates: false when nothing is parked. Entries destroyed behind the pool's back are skipped.</summary>
        public bool TryAcquire(out T entry)
        {
            ThrowIfDisposed();
            while (_available.Count > 0)
            {
                var next = _available.Dequeue();
                _parked.Remove(next);
                if (!_isAlive(next)) { _all.Remove(next); continue; }
                _setActive(next, true);
                (next as IPoolable)?.OnAcquireFromPool();
                entry = next;
                return true;
            }
            entry = null;
            return false;
        }

        public T Acquire()
        {
            if (TryAcquire(out var ready)) return ready;
            Log.Warning($"[{_label}] Pool exhausted, creating new instance.");
            var created = CreateTracked();
            _setActive(created, true);
            (created as IPoolable)?.OnAcquireFromPool();
            return created;
        }

        public void Return(T entry)
        {
            if (entry == null || _parked.Contains(entry)) return;
            if (!_all.Contains(entry)) throw new ArgumentException("The component belongs to another pool.", nameof(entry));
            if (!_isAlive(entry)) { _all.Remove(entry); return; }
            _reset?.Invoke(entry);
            (entry as IPoolable)?.OnReturnToPool();
            _setActive(entry, false);
            _park?.Invoke(entry);
            _available.Enqueue(entry);
            _parked.Add(entry);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var entry in _all)
                if (_isAlive(entry)) _destroy(entry);
            _all.Clear();
            _available.Clear();
            _parked.Clear();
        }

        private T CreateTracked()
        {
            var entry = _create() ?? throw new InvalidOperationException($"[{_label}] The create function returned null.");
            _all.Add(entry);
            return entry;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(_label);
        }
    }
}
