using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Adapter.Editor
{
    /// <summary>Writes the framework's committed assets (the overlay PanelSettings).</summary>
    public static class FrameworkAssets
    {
        public const string ResourcesRoot = "Packages/com.unidad.unity/Runtime/Resources";
        public const string OverlayPath = ResourcesRoot + "/Unidad/Panels/Overlay.asset";
        public const string ThemePath = ResourcesRoot + "/Unidad/Ui/Unidad.tss";
        public const int ReferenceWidth = 1920, ReferenceHeight = 1080;
        public const float Match = 0.5f;

        /// <summary>Overwriting keeps the asset's GUID: the existing asset takes the fresh one's fields.</summary>
        public static void Write() => BatchExit.Run("FrameworkAssets.Write", () =>
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) return $"no theme at {ThemePath}";

            var fresh = ScriptableObject.CreateInstance<PanelSettings>();
            fresh.themeStyleSheet = theme;
            fresh.renderMode = PanelRenderMode.ScreenSpaceOverlay;
            fresh.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            fresh.referenceResolution = new Vector2Int(ReferenceWidth, ReferenceHeight);
            fresh.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            fresh.match = Match;
            fresh.sortingOrder = 0;
            fresh.clearColor = false;

            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(OverlayPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = "Overlay";
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(fresh);
            }
            else
            {
                EditorFolders.Ensure(OverlayPath.Substring(0, OverlayPath.LastIndexOf('/')));
                AssetDatabase.CreateAsset(fresh, OverlayPath);
            }

            AssetDatabase.SaveAssets();
            return null;
        });
    }

    internal static class EditorFolders
    {
        public static void Ensure(string folder)
        {
            foreach (var (parent, child) in PicoCadPaths.FolderSteps(folder))
                if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
