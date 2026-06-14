using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unidad.Core.ModelCatalog.Effects;

namespace Unidad.Core.ModelCatalog
{
    /// <summary>
    /// Attached to every prefab root by <c>PicoCadPrefabBuilder</c>. Stores only the
    /// model's catalog identity (modelId/kindId); the previewable effect ids are
    /// enumerated live from the kind's registered <see cref="IModelEffectProfile"/>
    /// implementation and the baked Animator clips from the prefab's controller,
    /// so effects added later show up on existing prefabs without a rebuild.
    /// The custom inspector (Unidad.Core.ModelCatalog.Editor) renders one play
    /// button per effect/clip in play mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModelEffectPreview : MonoBehaviour
    {
        [Tooltip("Catalog model id (models.json).")]
        public string modelId;

        [Tooltip("Catalog kind id (kinds.json) — selects the effect profile to preview.")]
        public string kindId;

        // Built once per domain. A domain reload — i.e. any code change that could
        // add, remove, or alter an effect profile — rebuilds it, which is exactly
        // the moment the preview list can change.
        static Dictionary<string, IModelEffectProfile> _profilesById;

        /// <summary>The kind entry from Resources/ModelCatalog/kinds.json, or null.</summary>
        public ModelKindDefinition ResolveKind()
        {
            if (string.IsNullOrEmpty(kindId)) return null;
            var database = ModelCatalogDatabase.LoadFromResources();
            return database.Kinds.FirstOrDefault(k => k.id == kindId);
        }

        /// <summary>The kind's effect profile instance, resolved from any loaded assembly.</summary>
        public IModelEffectProfile ResolveProfile()
        {
            var profileId = ResolveKind()?.effectProfile;
            if (string.IsNullOrEmpty(profileId)) return null;
            var profiles = _profilesById ??= DiscoverProfiles();
            return profiles.TryGetValue(profileId, out var profile) ? profile : null;
        }

        /// <summary>
        /// Effect ids offered for preview: live from the profile implementation when
        /// it resolves, falling back to the kind's effects array in kinds.json.
        /// </summary>
        public string[] GetEffectIds()
        {
            var profile = ResolveProfile();
            if (profile != null) return profile.EffectIds.ToArray();
            return ResolveKind()?.effects ?? Array.Empty<string>();
        }

        /// <summary>Baked clip names, read live from the Animator's controller.</summary>
        public string[] GetClipNames()
        {
            var animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
                return Array.Empty<string>();
            return animator.runtimeAnimatorController.animationClips
                .Where(clip => clip != null)
                .Select(clip => clip.name)
                .Distinct()
                .ToArray();
        }

        /// <summary>Play a profile effect on this transform. Returns its duration.</summary>
        public float PlayEffect(string effectId)
        {
            var profile = ResolveProfile();
            return profile?.Play(effectId, transform) ?? 0f;
        }

        /// <summary>Play a baked clip state on the Animator (no-op when none).</summary>
        public void PlayClip(string stateName)
        {
            var animator = GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.Play(stateName);
        }

        static Dictionary<string, IModelEffectProfile> DiscoverProfiles()
        {
            var profiles = new Dictionary<string, IModelEffectProfile>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IModelEffectProfile).IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                    try
                    {
                        var profile = (IModelEffectProfile)Activator.CreateInstance(type);
                        if (!string.IsNullOrEmpty(profile.Id))
                            profiles[profile.Id] = profile;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ModelEffectPreview] Could not instantiate effect profile {type.FullName}: {e.Message}");
                    }
                }
            }
            return profiles;
        }
    }
}
