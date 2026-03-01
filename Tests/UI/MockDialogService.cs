using System;
using System.Collections.Generic;
using Unidad.Core.UI.Dialog;

namespace Unidad.Core.Tests.Tests.UI
{
    public sealed class MockDialogService : IDialogService
    {
        private readonly Queue<Action<DialogResult>> _pendingCallbacks = new();
        private readonly List<DialogDefinition> _shownDialogs = new();

        public DialogResult AutoResult { get; set; }
        public bool AutoResolve { get; set; }
        public bool HasActiveDialog => _pendingCallbacks.Count > 0;
        public IReadOnlyList<DialogDefinition> ShownDialogs => _shownDialogs;

        public void Show(DialogDefinition definition, Action<DialogResult> onResult = null)
        {
            _shownDialogs.Add(definition);

            if (AutoResolve && AutoResult != null)
            {
                onResult?.Invoke(AutoResult);
            }
            else if (onResult != null)
            {
                _pendingCallbacks.Enqueue(onResult);
            }
        }

        public void ShowConfirm(string title, string message, Action<DialogResult> onResult = null)
        {
            Show(new DialogDefinition(title, message,
                new[] { new DialogButton("Cancel", "cancel"), new DialogButton("Confirm", "confirm") }),
                onResult);
        }

        public void ShowAlert(string title, string message, Action onDismiss = null)
        {
            Show(new DialogDefinition(title, message,
                new[] { new DialogButton("OK", "ok") }),
                onDismiss != null ? _ => onDismiss() : null);
        }

        public void DismissCurrent()
        {
            if (_pendingCallbacks.Count > 0)
            {
                var callback = _pendingCallbacks.Dequeue();
                callback(DialogResult.Dismissed);
            }
        }

        public void DismissAll()
        {
            while (_pendingCallbacks.Count > 0)
                DismissCurrent();
        }

        public void ResolveWith(DialogResult result)
        {
            if (_pendingCallbacks.Count > 0)
            {
                var callback = _pendingCallbacks.Dequeue();
                callback(result);
            }
        }
    }
}
