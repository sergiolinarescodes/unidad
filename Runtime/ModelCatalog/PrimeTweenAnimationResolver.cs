using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using Unidad.Core.Abstractions;

namespace Unidad.Core.ModelCatalog
{
    /// <summary>
    /// Production IAnimationResolver for model effects. Animation ids are
    /// "instanceId/effectId": the target transform is resolved through the
    /// catalog service (wired by the installer), the kind's effect profile runs
    /// the PrimeTween effect, and onComplete fires after the returned duration.
    /// Tests swap in InstantAnimationResolver and never touch this class.
    /// </summary>
    internal sealed class PrimeTweenAnimationResolver : IAnimationResolver
    {
        readonly Dictionary<string, Effects.IModelEffectProfile> _profiles = new();

        /// <summary>Resolves "instanceId" -> (target transform, effect profile id). Wired by the installer.</summary>
        public Func<string, (Transform target, string profileId)?> InstanceResolver { get; set; }

        public bool IsInstant => false;

        public void RegisterProfile(Effects.IModelEffectProfile profile)
        {
            _profiles[profile.Id] = profile;
        }

        public void Play(string animationId, Action onComplete = null)
        {
            var separator = animationId.IndexOf('/');
            if (separator <= 0 || InstanceResolver == null)
            {
                onComplete?.Invoke();
                return;
            }

            var instanceId = animationId[..separator];
            var effectId = animationId[(separator + 1)..];
            var resolved = InstanceResolver(instanceId);
            if (resolved == null || resolved.Value.target == null ||
                !_profiles.TryGetValue(resolved.Value.profileId ?? string.Empty, out var profile))
            {
                onComplete?.Invoke();
                return;
            }

            var duration = profile.Play(effectId, resolved.Value.target);
            if (onComplete == null) return;
            if (duration <= 0f)
            {
                onComplete.Invoke();
                return;
            }
            Tween.Delay(duration, onComplete);
        }
    }
}
