using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace Unidad.Core.ModelCatalog.Effects
{
    /// <summary>
    /// Example effect profile: spawn pop, despawn shrink, and a hop.
    /// Serves as the template the pipeline's codegen follows for new kinds.
    /// </summary>
    internal sealed class BounceEffectProfile : IModelEffectProfile
    {
        public string Id => "bounce";

        public IEnumerable<string> EffectIds => new[] { "spawn", "despawn", "hop" };

        public float Play(string effectId, Transform target)
        {
            switch (effectId)
            {
                case "spawn":
                {
                    var endScale = target.localScale;
                    target.localScale = Vector3.zero;
                    Tween.Scale(target, endScale, 0.35f, Ease.OutBack);
                    return 0.35f;
                }
                case "despawn":
                    Tween.Scale(target, Vector3.zero, 0.25f, Ease.InBack);
                    return 0.25f;
                case "hop":
                    Tween.PositionY(target, target.position.y + 0.5f, 0.18f, Ease.OutQuad, 2, CycleMode.Yoyo);
                    return 0.72f;
                default:
                    return 0f;
            }
        }
    }
}
