namespace Unidad.Core.UI.Tooltip
{
    public sealed class SubTooltipEntry
    {
        public TooltipContent Content { get; }
        public TooltipStyle Style { get; }
        public TooltipPlacement PreferredPlacement { get; }

        public SubTooltipEntry(TooltipContent content,
            TooltipStyle style = null,
            TooltipPlacement preferredPlacement = TooltipPlacement.Right)
        {
            Content = content;
            Style = style;
            PreferredPlacement = preferredPlacement;
        }
    }
}
