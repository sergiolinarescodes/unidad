using System;
using System.Collections.Generic;
using Unidad.Adapter.Motion;
using Unidad.Core.Motion;
using UnityEngine;

namespace Unidad.Adapter.Models
{
    /// <summary>Runs on every spawned model, by order (a game's material swap); added with <c>AddMany&lt;IModelDecorator&gt;</c>.</summary>
    public interface IModelDecorator
    {
        void Decorate(GameObject instance, ModelEntry entry);
    }

    /// <summary>Model hit feedback on the motion runner (MaterialPropertyBlock, no material copies).</summary>
    public static class ModelEffects
    {
        public const float DefaultIntensity = 2f, DefaultDuration = 0.15f;

        /// <summary>Flashes every renderer under <paramref name="root"/>; returns the flash's length in seconds.</summary>
        public static float Flash(IMotionRunner runner, Transform root, Color color, float intensity = DefaultIntensity, float duration = DefaultDuration)
        {
            var spec = new MotionSpec(duration, Ease.OutQuad);
            var glow = new Color(color.r, color.g, color.b, 0f) * intensity;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (material == null) continue;
                var property = ColorProperty.Pick(material.HasProperty);
                if (property == null) continue;
                var id = Shader.PropertyToID(property);
                var original = material.GetColor(id);
                var target = MotionTargets.MpbColor(id);
                // the start cancels a running flash; from comes from the shared material, so re-triggers never drift
                target.Write(renderer, original + glow);
                runner.Play(renderer, target, original, spec, MotionChannel.Flash);
            }

            return duration;
        }
    }
}

namespace Unidad.Adapter
{
    using Unidad.Adapter.Models;

    /// <summary>The GameObject half of <c>IGameObjectFactory.InstantiateModel</c> (§c.10).</summary>
    internal static class ModelSpawner
    {
        /// <param name="instantiate">The factory's prefab instantiate by asset key.</param>
        public static GameObject Instantiate(IModelCatalog catalog, IReadOnlyList<IModelDecorator> decorators, string modelId,
            Func<string, GameObject> instantiate) =>
            ModelSpawn.Spawn(catalog, modelId, entry =>
            {
                var instance = instantiate(entry.AssetKey);
                instance.transform.localScale *= entry.Scale;
                return instance;
            }, decorators, static (decorator, instance, entry) => decorator.Decorate(instance, entry));
    }
}
