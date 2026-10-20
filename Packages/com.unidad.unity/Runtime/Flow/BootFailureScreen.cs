using System;
using Unidad.Adapter.Ui;
using Unidad.Core.DI;
using Unidad.Core.Diagnostics;
using Unidad.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Adapter
{
    /// <summary>The boot failure page (layer Error), or exit code 1 in batch mode.</summary>
    internal static class BootFailureScreen
    {
        public const string FallbackTitle = BootFailureText.FallbackTitle;

        public static void Show(MonoBehaviour scene, Container containerOrNull, Exception error)
        {
            if (Application.isBatchMode)
            {
                AppQuit.Now(1);
                return;
            }
            // Built by hand: the factory, the panel library or the container may be what failed.
            var assets = new AssetLibrary();
            PanelSettings settings;
            VisualTreeAsset page;
            StyleSheet tokens;
            try
            {
                using (assets.AllowColdLoads())
                {
                    settings = assets.Load<PanelSettings>(OverlayKey(containerOrNull));
                    page = assets.Load<VisualTreeAsset>(UiKeys.BootFailure);
                    tokens = assets.Load<StyleSheet>(UiKeys.Tokens);
                }
            }
            catch (Exception loadError)
            {
                Log.Error($"[Boot] The failure page could not load: {loadError.Message}");
                return;
            }
            if (settings == null || page == null)
            {
                Log.Error("[Boot] The failure page could not load its PanelSettings or UXML.");
                return;
            }

            var gameObject = new GameObject("BootFailure");
            gameObject.transform.SetParent(scene.transform, false);
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.sortingOrder = PanelOrder.Error;
            var root = document.rootVisualElement;
            if (tokens != null) root.styleSheets.Add(tokens);
            page.CloneTree(root);
            var message = root.Q<Label>("message");
            if (message != null)
                message.text = BootFailureText.Title(() => containerOrNull?.Resolve<ITextService>()) + "\n\n" + error?.Message;
        }

        private static string OverlayKey(Container containerOrNull)
        {
            try
            {
                return containerOrNull != null && containerOrNull.TryResolve<IPanelLibrary>(out var panels) ? panels.OverlayPanelKey : PanelKeys.Overlay;
            }
            catch (Exception)
            {
                return PanelKeys.Overlay;
            }
        }
    }
}
