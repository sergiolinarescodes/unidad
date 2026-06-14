using PrimeTween;
using UnityEngine;

namespace Unidad.Core.ModelCatalog.Effects
{
    /// <summary>
    /// Material helpers for effect profiles. Three pitfalls make naive material
    /// flashes silently do nothing on pipeline models, so profiles MUST go
    /// through this helper instead of tweening shader properties directly:
    /// 1. glTFast materials ("Shader Graphs/glTF-pbrMetallicRoughness") name
    ///    their properties baseColorFactor/emissiveFactor — not _BaseColor/_Color.
    /// 2. URP's SRP Batcher ignores per-material values set via
    ///    MaterialPropertyBlock, so tweens must target instanced materials.
    /// 3. emissiveFactor is multiplied by the emissive texture, which defaults to
    ///    black on glTFast materials — emission tweens render nothing. Overbright
    ///    the base color factor instead (texture * factor, factor &gt; 1 brightens).
    /// </summary>
    public static class ModelEffectUtility
    {
        static readonly int BaseColorFactorId = Shader.PropertyToID("baseColorFactor"); // glTFast shader graphs
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");            // URP/Lit
        static readonly int ColorId = Shader.PropertyToID("_Color");                    // built-in/legacy

        /// <summary>
        /// Overbright flash on every renderer under <paramref name="target"/>: the
        /// base color factor jumps to original + <paramref name="color"/> *
        /// <paramref name="intensity"/> and eases back. The factor multiplies the
        /// albedo texture, so texels saturate toward the flash color and ease back.
        /// Returns <paramref name="duration"/>.
        /// </summary>
        public static float Flash(Transform target, Color color, float intensity = 2f, float duration = 0.15f)
        {
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                // renderer.materials instantiates copies owned by the renderer —
                // intentional: per-instance flash that the SRP Batcher honors.
                foreach (var material in renderer.materials)
                {
                    var propertyId =
                        material.HasProperty(BaseColorFactorId) ? BaseColorFactorId :
                        material.HasProperty(BaseColorId) ? BaseColorId :
                        material.HasProperty(ColorId) ? ColorId : -1;
                    if (propertyId == -1) continue;

                    // Complete (not stop) any running flash so the original value is
                    // restored before we capture it — re-triggering mid-flash must not drift.
                    Tween.CompleteAll(onTarget: material);
                    var original = material.GetColor(propertyId);
                    var add = color;
                    add.a = 0f; // flash never touches alpha
                    var id = propertyId;
                    Tween.Custom(material, intensity, 0f, duration,
                            (m, value) => m.SetColor(id, original + add * value), Ease.OutQuad)
                        .OnComplete(material, m => m.SetColor(id, original));
                }
            }
            return duration;
        }
    }
}
