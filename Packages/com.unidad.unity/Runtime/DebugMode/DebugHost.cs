using System;
using System.Collections.Generic;
using Unidad.Adapter.Ui;
using Unidad.Core.DI;
using Unidad.Core.Diagnostics;
using Unidad.Core.EventBus;
using Unidad.Launch;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Unidad.Adapter
{
    /// <summary>The Alt+D debug window in development builds; release players bind the inactive debug mode.</summary>
    internal static class DebugHost
    {
        public static void Bind(Container container, IGameObjectFactory factory, IPanelLibrary panels, LaunchArgs launch)
        {
            if (!UnityDebug.isDebugBuild || !container.TryResolve<DebugModeState>(out var state)) return;
            var bus = container.Resolve<IEventBus>();
            var host = panels.Overlay("DebugWindow", PanelOrder.Debug, UiKeys.DebugWindow);
            var window = new DebugWindow(host, container.ResolveAll<IDebugSection>());
            void Mirror() => window.SetVisible(state.IsActive && !state.WindowsHidden);

            var owned = host.Document.gameObject.AddComponent<DisposeOnDestroy>();
            owned.Add(new DebugInputBridge(state));
            owned.Add(bus.Subscribe<DebugModeActivatedEvent>(_ => Mirror()));
            owned.Add(bus.Subscribe<DebugModeDeactivatedEvent>(_ => Mirror()));
            owned.Add(bus.Subscribe<CloseAllDebugWindowsEvent>(_ => Mirror()));
            Mirror();
            if (launch.IsOn(LaunchSettings.DebugOpen)) state.Activate();
        }
    }

    /// <summary>Disposes what a plain host owns (input actions, bus subscriptions) when its GameObject dies with the scene.</summary>
    internal sealed class DisposeOnDestroy : MonoBehaviour
    {
        private readonly List<IDisposable> _owned = new();

        public void Add(IDisposable disposable) => _owned.Add(disposable);

        private void OnDestroy()
        {
            for (int i = _owned.Count - 1; i >= 0; i--) _owned[i].Dispose();
            _owned.Clear();
        }
    }
}
