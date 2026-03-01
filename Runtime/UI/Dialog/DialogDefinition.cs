namespace Unidad.Core.UI.Dialog
{
    public sealed class DialogDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public string Body { get; }
        public DialogButton[] Buttons { get; }
        public string AnimationPreset { get; }
        public bool SkipOnClick { get; }

        public DialogDefinition(
            string title,
            string body,
            DialogButton[] buttons,
            string id = null,
            string animationPreset = "dialog",
            bool skipOnClick = true)
        {
            Id = id ?? System.Guid.NewGuid().ToString("N")[..8];
            Title = title;
            Body = body;
            Buttons = buttons ?? System.Array.Empty<DialogButton>();
            AnimationPreset = animationPreset;
            SkipOnClick = skipOnClick;
        }
    }
}
