using System;

namespace Unidad.Core.UI.Dialog
{
    public interface IDialogService
    {
        void Show(DialogDefinition definition, Action<DialogResult> onResult = null);
        void ShowConfirm(string title, string message, Action<DialogResult> onResult = null);
        void ShowAlert(string title, string message, Action onDismiss = null);
        void DismissCurrent();
        void DismissAll();
        bool HasActiveDialog { get; }
    }
}
