using Unidad.Core.UI.Components;

namespace Unidad.Core.UI.Dialog
{
    public sealed class DialogButton
    {
        public string Label { get; }
        public string Id { get; }
        public ButtonVariant Variant { get; }

        public DialogButton(string label, string id = null, ButtonVariant variant = ButtonVariant.Primary)
        {
            Label = label;
            Id = id ?? label.ToLower().Replace(" ", "-");
            Variant = variant;
        }
    }
}
