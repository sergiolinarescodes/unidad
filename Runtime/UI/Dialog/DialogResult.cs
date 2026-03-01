namespace Unidad.Core.UI.Dialog
{
    public sealed class DialogResult
    {
        public string ButtonId { get; }
        public string DialogId { get; }

        public DialogResult(string buttonId, string dialogId)
        {
            ButtonId = buttonId;
            DialogId = dialogId;
        }

        public static readonly DialogResult Dismissed = new("dismissed", "");
    }
}
