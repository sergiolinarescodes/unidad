using UnityEngine;

namespace Unidad.Core.UI.Tooltip
{
    public static class TooltipPositioner
    {
        public readonly record struct PositionResult(
            Vector2 Position,
            TooltipPlacement Placement,
            Vector2 ArrowOffset);

        private const float Margin = 4f;

        public static PositionResult Compute(
            Rect anchorRect,
            Vector2 tooltipSize,
            Vector2 containerSize,
            TooltipPlacement preferred,
            float arrowSize = 8f)
        {
            var candidates = BuildCandidateOrder(preferred);
            var anchorCenter = anchorRect.center;
            var offset = arrowSize + Margin;

            foreach (var candidate in candidates)
            {
                var pos = ComputePosition(candidate, anchorRect, anchorCenter, tooltipSize, offset);

                if (FitsInContainer(pos, tooltipSize, containerSize))
                {
                    var arrowOffset = ComputeArrowOffset(candidate, anchorCenter, pos, tooltipSize);
                    return new PositionResult(pos, candidate, arrowOffset);
                }
            }

            // Nothing fits — use preferred direction + clamp
            var fallbackPos = ComputePosition(preferred == TooltipPlacement.Auto ? TooltipPlacement.Bottom : preferred,
                anchorRect, anchorCenter, tooltipSize, offset);
            fallbackPos = ClampToContainer(fallbackPos, tooltipSize, containerSize);
            var fallbackPlacement = preferred == TooltipPlacement.Auto ? TooltipPlacement.Bottom : preferred;
            var fallbackArrow = ComputeArrowOffset(fallbackPlacement, anchorCenter, fallbackPos, tooltipSize);
            return new PositionResult(fallbackPos, fallbackPlacement, fallbackArrow);
        }

        private static TooltipPlacement[] BuildCandidateOrder(TooltipPlacement preferred)
        {
            if (preferred == TooltipPlacement.Auto)
                return new[] { TooltipPlacement.Bottom, TooltipPlacement.Top, TooltipPlacement.Right, TooltipPlacement.Left };

            return preferred switch
            {
                TooltipPlacement.Top => new[] { TooltipPlacement.Top, TooltipPlacement.Bottom, TooltipPlacement.Right, TooltipPlacement.Left },
                TooltipPlacement.Bottom => new[] { TooltipPlacement.Bottom, TooltipPlacement.Top, TooltipPlacement.Right, TooltipPlacement.Left },
                TooltipPlacement.Left => new[] { TooltipPlacement.Left, TooltipPlacement.Right, TooltipPlacement.Bottom, TooltipPlacement.Top },
                TooltipPlacement.Right => new[] { TooltipPlacement.Right, TooltipPlacement.Left, TooltipPlacement.Bottom, TooltipPlacement.Top },
                _ => new[] { TooltipPlacement.Bottom, TooltipPlacement.Top, TooltipPlacement.Right, TooltipPlacement.Left }
            };
        }

        private static Vector2 ComputePosition(
            TooltipPlacement placement,
            Rect anchorRect,
            Vector2 anchorCenter,
            Vector2 tooltipSize,
            float offset)
        {
            return placement switch
            {
                TooltipPlacement.Top => new Vector2(
                    anchorCenter.x - tooltipSize.x * 0.5f,
                    anchorRect.yMin - tooltipSize.y - offset),

                TooltipPlacement.Bottom => new Vector2(
                    anchorCenter.x - tooltipSize.x * 0.5f,
                    anchorRect.yMax + offset),

                TooltipPlacement.Left => new Vector2(
                    anchorRect.xMin - tooltipSize.x - offset,
                    anchorCenter.y - tooltipSize.y * 0.5f),

                TooltipPlacement.Right => new Vector2(
                    anchorRect.xMax + offset,
                    anchorCenter.y - tooltipSize.y * 0.5f),

                _ => new Vector2(
                    anchorCenter.x - tooltipSize.x * 0.5f,
                    anchorRect.yMax + offset)
            };
        }

        private static bool FitsInContainer(Vector2 pos, Vector2 size, Vector2 container)
        {
            return pos.x >= 0 && pos.y >= 0 &&
                   pos.x + size.x <= container.x &&
                   pos.y + size.y <= container.y;
        }

        private static Vector2 ClampToContainer(Vector2 pos, Vector2 size, Vector2 container)
        {
            pos.x = Mathf.Clamp(pos.x, 0, Mathf.Max(0, container.x - size.x));
            pos.y = Mathf.Clamp(pos.y, 0, Mathf.Max(0, container.y - size.y));
            return pos;
        }

        private static Vector2 ComputeArrowOffset(
            TooltipPlacement placement,
            Vector2 anchorCenter,
            Vector2 tooltipPos,
            Vector2 tooltipSize)
        {
            // Arrow offset = how far from the tooltip edge center the arrow must shift to point at anchor center
            return placement switch
            {
                TooltipPlacement.Top or TooltipPlacement.Bottom =>
                    new Vector2(anchorCenter.x - (tooltipPos.x + tooltipSize.x * 0.5f), 0),

                TooltipPlacement.Left or TooltipPlacement.Right =>
                    new Vector2(0, anchorCenter.y - (tooltipPos.y + tooltipSize.y * 0.5f)),

                _ => Vector2.zero
            };
        }
    }
}
