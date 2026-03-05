using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Tooltip
{
    public sealed class WorldTooltipHandle
    {
        internal int Id { get; }
        internal GameObject Go { get; }
        internal UIDocument Document { get; }
        internal VisualElement CachedContainer { get; set; }

        internal WorldTooltipHandle(int id, GameObject go, UIDocument document)
        {
            Id = id;
            Go = go;
            Document = document;
        }
    }
}
