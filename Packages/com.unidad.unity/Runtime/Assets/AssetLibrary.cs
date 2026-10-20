using System;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>
    /// The one place assets are loaded, by Resources-relative key. Once sealed (the flow entered Play), a key that was never
    /// loaded throws "Unprepared asset '&lt;key&gt;'. Register it with ScenePreparation before gameplay."
    /// </summary>
    public sealed class AssetLibrary
    {
        private readonly AssetShelf _shelf = new();

        public T Load<T>(string key) where T : UnityEngine.Object => (T)_shelf.Get(key, typeof(T), () => LoadResource<T>(key));

        /// <summary>A miss is cached and returns false.</summary>
        public bool TryLoad<T>(string key, out T asset) where T : UnityEngine.Object
        {
            var found = _shelf.TryGet(key, typeof(T), () => LoadResource<T>(key), out var loaded);
            asset = found ? (T)loaded : null;
            return found;
        }

        public IDisposable AllowColdLoads() => _shelf.AllowColdLoads();

        public void BeginScene() => _shelf.BeginScene();

        public void Seal() => _shelf.Seal();

        public bool IsSealed => _shelf.IsSealed;

        public int Count => _shelf.Count;

        // Unity's fake null becomes a real null, so the shelf caches the miss.
        private static object LoadResource<T>(string key) where T : UnityEngine.Object
        {
            var asset = Resources.Load<T>(key);
            return asset != null ? asset : null;
        }
    }
}
