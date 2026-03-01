using System;

namespace Unidad.Core.EventBus
{
    /// <summary>
    /// Lightweight IDisposable that invokes an Action on dispose.
    /// Used for subscription tokens across EventBus, MockEventBus, TestEventBus.
    /// </summary>
    public sealed class ActionDisposable : IDisposable
    {
        private Action _onDispose;

        public ActionDisposable(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            var action = _onDispose;
            _onDispose = null;
            action?.Invoke();
        }
    }
}
