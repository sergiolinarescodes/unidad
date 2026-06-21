using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Tooltip
{
    public interface ITooltipService
    {
        TooltipHandle Show(TooltipContent content, TooltipAnchor anchor,
            TooltipPlacement placement = TooltipPlacement.Auto, TooltipStyle style = null);

        void Hide(TooltipHandle handle);
        void HideAll();

        IDisposable RegisterHover(VisualElement target, TooltipContent content,
            TooltipPlacement placement = TooltipPlacement.Auto,
            TooltipStyle style = null, float delayMs = 400f);

        void SetTooltipLayer(VisualElement layer);

        /// <summary>
        /// Optional per-tooltip border painter. When set, each screen-space tooltip's flat style border is
        /// suppressed and this is invoked on the container so a host can paint its own border (e.g. the HUD's
        /// fine-frame) — keeping the package free of any game-specific border code.
        /// </summary>
        void SetFrameDecorator(Action<VisualElement> decorator);

        void Attach(GameObject target, string text, TooltipStyle style = null,
            Vector3 offset = default, WorldTooltipCollision collision = WorldTooltipCollision.None,
            WorldTooltipShowMode showMode = WorldTooltipShowMode.FadeIn);
        void Detach(GameObject target);

        void Attach(VisualElement target, string text, TooltipStyle style = null,
            TooltipPlacement placement = TooltipPlacement.Auto, float delayMs = 400f);
        void Detach(VisualElement target);
    }
}
