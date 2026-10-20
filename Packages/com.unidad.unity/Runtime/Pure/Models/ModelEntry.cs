using System;
using System.Collections.Generic;

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

    /// <summary>Every model of every source, by id.</summary>
    public interface IModelCatalog
    {
        IReadOnlyList<string> Ids { get; }

        bool Has(string id);

        ModelEntry Get(string id);
    }
}

namespace Unidad.Adapter
{
    using Unidad.Adapter.Models;

    /// <summary><c>InstantiateModel</c>'s order, engine-free: entry, instantiate (scaled), then every decorator in resolve order.</summary>
    internal static class ModelSpawn
    {
        public static TInstance Spawn<TInstance, TDecorator>(IModelCatalog catalog, string modelId, Func<ModelEntry, TInstance> instantiate,
            IReadOnlyList<TDecorator> decorators, Action<TDecorator, TInstance, ModelEntry> decorate)
        {
            var entry = catalog.Get(modelId);
            var instance = instantiate(entry);
            foreach (var decorator in decorators) decorate(decorator, instance, entry);
            return instance;
        }
    }

    /// <summary>The colour property a model flash drives: glTFast's, then URP's, then the legacy one.</summary>
    internal static class ColorProperty
    {
        public const string Gltf = "baseColorFactor", Urp = "_BaseColor", Legacy = "_Color";

        /// <summary>The first of glTF, URP, legacy that <paramref name="has"/> reports; null when none.</summary>
        public static string Pick(Func<string, bool> has) =>
            has(Gltf) ? Gltf : has(Urp) ? Urp : has(Legacy) ? Legacy : null;
    }
}
