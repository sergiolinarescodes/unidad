using System;
using System.Collections.Generic;
using Unidad.Adapter.Motion;
using UnityEngine;

namespace Unidad.Adapter.Models
{
    /// <summary>One model: its id, the AssetLibrary key of its prefab, a normalising scale and a kind.</summary>
    public sealed record ModelEntry(string Id, string AssetKey, float Scale = 1f, string Kind = null);

    /// <summary>Feeds the catalogue; added with <c>AddMany&lt;IModelSource&gt;</c>.</summary>
    public interface IModelSource
    {
        string Source { get; }

        IReadOnlyList<ModelEntry> Models { get; }
    }

    /// <summary>Runs on every spawned model, by order (a game's material swap); added with <c>AddMany&lt;IModelDecorator&gt;</c>.</summary>
    public interface IModelDecorator
    {
        void Decorate(GameObject instance, ModelEntry entry);
    }

    /// <summary>Every model of every source, by id.</summary>
    public interface IModelCatalog
    {
        IReadOnlyList<string> Ids { get; }

        bool Has(string id);

        ModelEntry Get(string id);
    }

    /// <summary>Model hit feedback on the motion runner (MaterialPropertyBlock, no material copies).</summary>
    public static class ModelEffects
    {
        public const float DefaultIntensity = 2f, DefaultDuration = 0.15f;

        /// <summary>Flashes every renderer under <paramref name="root"/>; returns the flash's length in seconds.</summary>
        public static float Flash(IMotionRunner runner, Transform root, Color color, float intensity = DefaultIntensity, float duration = DefaultDuration)
            => throw new NotImplementedException();
    }
}
