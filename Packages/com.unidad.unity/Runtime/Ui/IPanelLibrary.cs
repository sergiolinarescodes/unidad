using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Adapter.Ui
{
    public static class PanelKeys
    {
        /// <summary>The AssetLibrary key of the framework's overlay PanelSettings.</summary>
        public const string Overlay = "Unidad/Panels/Overlay";
    }

    /// <summary>Framework panel layers (UIDocument sort orders on the one overlay PanelSettings); games add theirs between.</summary>
    public static class PanelOrder
    {
        public const float Hud = 10, Debug = 50, Pause = 100, Curtain = 128, Error = 200;
    }

    /// <summary>The framework's UI files under Runtime/Resources.</summary>
    public static class UiKeys
    {
        public const string Theme = "Unidad/Ui/Unidad", Tokens = "Unidad/Ui/unidad", Curtain = "Unidad/Ui/LoadingCurtain",
            PauseMenu = "Unidad/Ui/PauseMenu", BootFailure = "Unidad/Ui/BootFailure", SaveNotice = "Unidad/Ui/SaveNotice",
            DebugWindow = "Unidad/Ui/DebugWindow";
    }

    /// <summary>Opens UI Toolkit documents: screen-space on the overlay PanelSettings, or world-space at an anchor.</summary>
    public interface IPanelLibrary
    {
        string OverlayPanelKey { get; }

        UiDocumentHost Overlay(string name, float sortOrder, string uxmlKey = null, params string[] ussKeys);

        UiDocumentHost World(string name, string panelKey, Vector2 sizePixels, Transform anchor, string uxmlKey = null, params string[] ussKeys);

        void Close(UiDocumentHost host);

        IReadOnlyList<UiDocumentHost> Open { get; }

        /// <summary>Input System screen coordinates (origin bottom-left); screen-space documents only.</summary>
        bool IsPointerOverUi(Vector2 screenPosition);
    }

    /// <summary>One open document and its root.</summary>
    public sealed class UiDocumentHost
    {
        public string Name => throw new NotImplementedException();

        public UIDocument Document => throw new NotImplementedException();

        public VisualElement Root => throw new NotImplementedException();

        /// <summary>The UXML key the document came from, for error messages.</summary>
        public string Source => throw new NotImplementedException();

        /// <summary>A missing element throws InvalidOperationException naming it, its type and <see cref="Source"/>.</summary>
        public T Q<T>(string name) where T : VisualElement => throw new NotImplementedException();

        public void SetVisible(bool visible) => throw new NotImplementedException();
    }
}
