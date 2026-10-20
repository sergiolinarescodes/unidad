using System;
using System.Collections.Generic;

namespace Unidad.Core.EventBus
{
    /// <summary>
    /// Collects multiple IDisposable instances and disposes them together.
    /// Standard pattern for managing subscription lifetimes.
    /// </summary>
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables = new();
        private bool _disposed;

        public void Add(IDisposable disposable)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CompositeDisposable));
            if (disposable == null)
                throw new ArgumentNullException(nameof(disposable));
            _disposables.Add(disposable);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var d in _disposables)
                d.Dispose();
            _disposables.Clear();
        }
    }
}
