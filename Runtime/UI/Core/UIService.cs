using System;
using System.Collections.Generic;
using System.Linq;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;
using Unidad.Core.UI.Events;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Core
{
    internal sealed class UIService : SystemServiceBase, IUIService
    {
        private readonly Dictionary<Type, IPanelController> _panels = new();
        private readonly Dictionary<UILayer, VisualElement> _layerRoots = new();
        private readonly UIDocument _rootDocument;

        public UIService(IEventBus eventBus, UIDocument rootDocument) : base(eventBus)
        {
            _rootDocument = rootDocument;
            InitializeLayers();
        }

        private void InitializeLayers()
        {
            if (_rootDocument == null) return;

            var root = _rootDocument.rootVisualElement;
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                var layerRoot = new VisualElement
                {
                    name = $"layer-{layer.ToString().ToLower()}",
                    pickingMode = PickingMode.Ignore
                };
                layerRoot.style.position = Position.Absolute;
                layerRoot.style.left = 0;
                layerRoot.style.right = 0;
                layerRoot.style.top = 0;
                layerRoot.style.bottom = 0;
                root.Add(layerRoot);
                _layerRoots[layer] = layerRoot;
            }
        }

        public void Register(IPanelController panel)
        {
            _panels[panel.GetType()] = panel;
        }

        public T Show<T>(object model = null) where T : class, IPanelController
        {
            var panel = Get<T>();
            if (panel == null) return null;

            if (_layerRoots.TryGetValue(panel.Layer, out var layerRoot))
            {
                if (panel.Root == null || panel.Root.parent == null)
                {
                    panel.Show(model);
                    layerRoot.Add(panel.Root);
                }
                else
                {
                    panel.Show(model);
                }
            }
            else
            {
                panel.Show(model);
            }

            Publish(new PanelShownEvent(typeof(T), panel.Layer));
            return panel;
        }

        public void Hide<T>() where T : class, IPanelController
        {
            var panel = Get<T>();
            if (panel == null || !panel.IsVisible) return;

            var layer = panel.Layer;
            panel.Hide();
            Publish(new PanelHiddenEvent(typeof(T), layer));
        }

        public void HideAll(UILayer layer)
        {
            foreach (var panel in _panels.Values.Where(p => p.Layer == layer && p.IsVisible))
            {
                panel.Hide();
                Publish(new PanelHiddenEvent(panel.GetType(), layer));
            }
        }

        public bool IsVisible<T>() where T : class, IPanelController
        {
            return Get<T>()?.IsVisible ?? false;
        }

        public T Get<T>() where T : class, IPanelController
        {
            return _panels.TryGetValue(typeof(T), out var panel) ? panel as T : null;
        }

        public override void Dispose()
        {
            foreach (var panel in _panels.Values)
                panel.Dispose();
            _panels.Clear();
            base.Dispose();
        }
    }
}
