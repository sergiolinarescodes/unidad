using UnityEngine;

namespace Unidad.Core.Testing
{
    /// <summary>
    /// Shared utility methods for visual test scenarios.
    /// </summary>
    public static class ScenarioHelpers
    {
        private static Shader _cachedShader;

        /// <summary>
        /// Sets the color of a GameObject's renderer using URP Lit material.
        /// Creates a new material instance to avoid shared-material side effects.
        /// </summary>
        public static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            if (_cachedShader == null)
                _cachedShader = Shader.Find("Universal Render Pipeline/Lit");

            var mat = new Material(_cachedShader) { color = color };
            renderer.sharedMaterial = mat;
        }
    }
}
