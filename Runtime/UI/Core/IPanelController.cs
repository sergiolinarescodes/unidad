using System;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Core
{
    public interface IPanelController : IDisposable
    {
        string PanelId { get; }
        UILayer Layer { get; }
        bool IsVisible { get; }
        VisualElement Root { get; }

        void Show(object model = null);
        void Hide();
        void OnLayerChanged(UILayer layer);
    }
}
