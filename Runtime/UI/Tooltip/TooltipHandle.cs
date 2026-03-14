using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Tooltip
{
    public sealed class TooltipHandle
    {
        internal int Id { get; }
        internal VisualElement Root { get; }
        internal VisualElement Arrow { get; }
        internal TooltipPlacement ResolvedPlacement { get; set; }
        internal List<TooltipHandle> SubHandles { get; } = new();
        internal IVisualElementScheduledItem SubTooltipTimer { get; set; }

        internal TooltipHandle(int id, VisualElement root, VisualElement arrow)
        {
            Id = id;
            Root = root;
            Arrow = arrow;
        }
    }
}
