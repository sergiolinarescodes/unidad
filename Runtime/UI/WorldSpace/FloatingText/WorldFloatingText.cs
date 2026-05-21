using Unidad.Core.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.WorldSpace
{
    /// <summary>
    /// Thin static facade over <see cref="IWorldFloatingTextService"/>.
    /// Preserves the ergonomic <c>WorldFloatingText.Spawn(...)</c> API for scenario code.
    /// </summary>
    public static class WorldFloatingText
    {
        private static IWorldFloatingTextService s_instance;

        /// <summary>
        /// Set (or clear) the backing service instance.
        /// Call with <c>null</c> during cleanup.
        /// </summary>
        public static void SetInstance(IWorldFloatingTextService instance) => s_instance = instance;

        public static void Initialize(
            PanelSettings panelSettings,
            VisualTreeAsset template = null,
            int prewarmCount = 10)
        {
            s_instance?.Initialize(panelSettings, template, prewarmCount);
        }

        public static void Spawn(
            Vector3 worldPosition,
            string text,
            FloatingTextStyle style = null,
            FloatingTextAnimator animator = null)
        {
            if (s_instance == null)
            {
                Debug.LogWarning("[WorldFloatingText] No service instance set. Call SetInstance() first.");
                return;
            }

            s_instance.Spawn(worldPosition, text, style, animator);
        }

        public static void Dispose()
        {
            s_instance?.Dispose();
            s_instance = null;
        }
    }
}
