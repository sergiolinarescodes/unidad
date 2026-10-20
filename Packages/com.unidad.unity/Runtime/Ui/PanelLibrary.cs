using System;
using System.Collections.Generic;
using Unidad.Adapter.Ui;
using UnityEngine;

namespace Unidad.Adapter
{
    internal sealed class PanelLibrary : IPanelLibrary
    {
        public PanelLibrary(IGameObjectFactory factory, AssetLibrary assets, string overlayPanelKey) => throw new NotImplementedException();

        public string OverlayPanelKey => throw new NotImplementedException();

        public UiDocumentHost Overlay(string name, float sortOrder, string uxmlKey = null, params string[] ussKeys) => throw new NotImplementedException();

        public UiDocumentHost World(string name, string panelKey, Vector2 sizePixels, Transform anchor, string uxmlKey = null, params string[] ussKeys)
            => throw new NotImplementedException();

        public void Close(UiDocumentHost host) => throw new NotImplementedException();

        public IReadOnlyList<UiDocumentHost> Open => throw new NotImplementedException();

        public bool IsPointerOverUi(Vector2 screenPosition) => throw new NotImplementedException();
    }
}
