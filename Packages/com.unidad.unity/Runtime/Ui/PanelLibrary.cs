using System;
using System.Collections.Generic;
using Unidad.Adapter.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Adapter
{
    internal sealed class PanelLibrary : IPanelLibrary
    {
        private readonly IGameObjectFactory _factory;
        private readonly AssetLibrary _assets;
        private readonly List<UiDocumentHost> _open = new();

        public PanelLibrary(IGameObjectFactory factory, AssetLibrary assets, string overlayPanelKey)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            if (string.IsNullOrEmpty(overlayPanelKey)) throw new ArgumentException("The overlay PanelSettings key is required.", nameof(overlayPanelKey));
            OverlayPanelKey = overlayPanelKey;
        }

        public string OverlayPanelKey { get; }

        public IReadOnlyList<UiDocumentHost> Open => _open;

        public UiDocumentHost Overlay(string name, float sortOrder, string uxmlKey = null, params string[] ussKeys)
            => Build(name, OverlayPanelKey, sortOrder, null, null, uxmlKey, ussKeys);

        public UiDocumentHost World(string name, string panelKey, Vector2 sizePixels, Transform anchor, string uxmlKey = null, params string[] ussKeys)
        {
            if (string.IsNullOrEmpty(panelKey)) throw new ArgumentException("A world-space document needs the game's own PanelSettings key.", nameof(panelKey));
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            return Build(name, panelKey, 0f, anchor, document =>
            {
                document.worldSpaceSizeMode = UnityEngine.UIElements.WorldSpaceSizeMode.Fixed;
                document.worldSpaceSize = sizePixels;
            }, uxmlKey, ussKeys);
        }

        public void Close(UiDocumentHost host)
        {
            if (host == null || !_open.Remove(host)) return;
            if (host.Document != null) _factory.Destroy(host.Document.gameObject);
        }

        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            var panelPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            foreach (var host in _open)
            {
                if (host.IsWorldSpace || host.Document == null) continue;
                var panel = host.Root?.panel;
                if (panel == null) continue;
                var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, panelPosition));
                if (picked != null && picked.pickingMode != PickingMode.Ignore) return true;
            }
            return false;
        }

        private UiDocumentHost Build(string name, string panelKey, float sortOrder, Transform parent, Action<UIDocument> configure,
            string uxmlKey, string[] ussKeys)
        {
            var gameObject = _factory.Create(name, parent);
            _factory.SetActive(gameObject, false);
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = _assets.Load<PanelSettings>(panelKey);
            // A fake-null asset is stored as null without a word (probe Q5): read it back.
            if (document.panelSettings == null)
            {
                _factory.Destroy(gameObject);
                throw new InvalidOperationException($"PanelSettings '{panelKey}' did not load.");
            }
            document.sortingOrder = sortOrder;
            configure?.Invoke(document);
            _factory.SetActive(gameObject, true);

            var root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.styleSheets.Add(_assets.Load<StyleSheet>(UiKeys.Tokens));
            if (uxmlKey != null) _assets.Load<VisualTreeAsset>(uxmlKey).CloneTree(root);
            if (ussKeys != null)
                foreach (var key in ussKeys) root.styleSheets.Add(_assets.Load<StyleSheet>(key));

            var host = new UiDocumentHost(name, document, uxmlKey, parent != null);
            _open.Add(host);
            return host;
        }
    }
}
