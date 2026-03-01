using System;
using System.Collections.Generic;
using Unidad.Core.UI.Core;

namespace Unidad.Core.Tests.Tests.UI
{
    public sealed class MockUIService : IUIService
    {
        private readonly Dictionary<Type, IPanelController> _panels = new();
        private readonly List<(Type Type, string Action)> _history = new();

        public IReadOnlyList<(Type Type, string Action)> History => _history;

        public void Register(IPanelController panel)
        {
            _panels[panel.GetType()] = panel;
        }

        public T Show<T>(object model = null) where T : class, IPanelController
        {
            _history.Add((typeof(T), "Show"));
            var panel = Get<T>();
            panel?.Show(model);
            return panel;
        }

        public void Hide<T>() where T : class, IPanelController
        {
            _history.Add((typeof(T), "Hide"));
            Get<T>()?.Hide();
        }

        public void HideAll(UILayer layer)
        {
            _history.Add((typeof(void), $"HideAll({layer})"));
        }

        public bool IsVisible<T>() where T : class, IPanelController
        {
            return Get<T>()?.IsVisible ?? false;
        }

        public T Get<T>() where T : class, IPanelController
        {
            return _panels.TryGetValue(typeof(T), out var panel) ? panel as T : null;
        }
    }
}
