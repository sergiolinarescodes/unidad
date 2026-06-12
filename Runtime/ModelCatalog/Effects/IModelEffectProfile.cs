using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.ModelCatalog.Effects
{
    /// <summary>
    /// Code-driven (PrimeTween) effect set for a model kind. Plain C# — registered
    /// by id in the installer, invoked only through PrimeTweenAnimationResolver so
    /// tests (InstantAnimationResolver) never run tweens.
    /// </summary>
    public interface IModelEffectProfile
    {
        /// <summary>Registry id, referenced by ModelKindDefinition.effectProfile.</summary>
        string Id { get; }

        IEnumerable<string> EffectIds { get; }

        /// <summary>Run the effect on a target transform. Returns its duration in seconds.</summary>
        float Play(string effectId, Transform target);
    }
}
