using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.WorldSpace
{
    public static class WorldSpaceUIFactory
    {
        private static readonly Vector2 DefaultWorldSpaceSize = new(1920, 1080);

        public static UIDocument Create(
            string name,
            Transform parent,
            PanelSettings panelSettings,
            VisualTreeAsset template = null,
            Vector2? worldSpaceSize = null,
            bool disablePicking = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);

            var uiDoc = go.AddComponent<UIDocument>();

            if (template != null)
                uiDoc.visualTreeAsset = template;

            if (panelSettings != null)
                uiDoc.panelSettings = panelSettings;

            uiDoc.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            uiDoc.worldSpaceSize = worldSpaceSize ?? DefaultWorldSpaceSize;

            if (disablePicking)
            {
                var root = uiDoc.rootVisualElement;
                if (root != null)
                    SetPickingModeRecursive(root, PickingMode.Ignore);
            }

            return uiDoc;
        }

        public static void SetPickingModeRecursive(VisualElement element, PickingMode mode)
        {
            element.pickingMode = mode;
            foreach (var child in element.Children())
            {
                SetPickingModeRecursive(child, mode);
            }
        }
    }
}
