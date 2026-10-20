using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;

namespace Unidad.Adapter
{
    /// <summary>The AssetLibrary's bookkeeping: a cache keyed by (key, type) that also remembers misses, and the seal rule.</summary>
    internal sealed class AssetShelf
    {
        private readonly Dictionary<(string Key, Type Type), object> _cache = new();
        private int _coldScopes;

        public bool IsSealed { get; private set; }

        public int Count => _cache.Count;

        /// <summary>Scopes nest; disposing one restores the count it found.</summary>
        public IDisposable AllowColdLoads()
        {
            var previous = _coldScopes;
            _coldScopes = previous + 1;
            return new ActionDisposable(() => _coldScopes = previous);
        }

        public void BeginScene() => IsSealed = false;

        public void Seal() => IsSealed = true;

        /// <summary>A hit (or a cached miss) never calls <paramref name="load"/>; a cold load while sealed throws naming the key.</summary>
        public bool TryGet(string key, Type type, Func<object> load, out object asset)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("An asset key is required.", nameof(key));
            if (_cache.TryGetValue((key, type), out asset)) return asset != null;
            if (IsSealed && _coldScopes == 0)
                throw new InvalidOperationException($"Unprepared asset '{key}'. Register it with ScenePreparation before gameplay.");
            asset = load();
            _cache[(key, type)] = asset;
            return asset != null;
        }

        public object Get(string key, Type type, Func<object> load) =>
            TryGet(key, type, load, out var asset) ? asset : throw new InvalidOperationException($"Asset did not load: {key} ({type.Name}).");
    }
}
