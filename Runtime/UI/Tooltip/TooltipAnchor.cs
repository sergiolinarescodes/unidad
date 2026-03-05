using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Tooltip
{
    public sealed class TooltipAnchor
    {
        private readonly VisualElement _element;
        private readonly Vector2? _screenPosition;
        private readonly Vector3? _worldPosition;

        private TooltipAnchor(VisualElement element, Vector2? screenPos, Vector3? worldPos)
        {
            _element = element;
            _screenPosition = screenPos;
            _worldPosition = worldPos;
        }

        public static TooltipAnchor FromElement(VisualElement element) => new(element, null, null);

        public static TooltipAnchor FromScreenPosition(Vector2 position) => new(null, position, null);

        public static TooltipAnchor FromWorldPosition(Vector3 worldPosition) => new(null, null, worldPosition);

        public Rect ResolveScreenRect(VisualElement panelRoot)
        {
            if (_element != null)
                return _element.worldBound;

            if (_screenPosition.HasValue)
            {
                var pos = _screenPosition.Value;
                return new Rect(pos.x, pos.y, 0, 0);
            }

            if (_worldPosition.HasValue)
            {
                var cam = Camera.main;
                if (cam == null)
                    return new Rect(0, 0, 0, 0);

                var screenPoint = cam.WorldToScreenPoint(_worldPosition.Value);

                // Flip Y: Unity screen coords have origin at bottom-left, UI Toolkit at top-left
                if (panelRoot?.panel != null)
                {
                    var panelHeight = panelRoot.resolvedStyle.height;
                    screenPoint.y = panelHeight - screenPoint.y;
                }

                return new Rect(screenPoint.x, screenPoint.y, 0, 0);
            }

            return new Rect(0, 0, 0, 0);
        }
    }
}
