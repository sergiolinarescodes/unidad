using System;

namespace Unidad.Adapter
{
    /// <summary>
    /// The one place assets are loaded, by Resources-relative key. Once sealed (the flow entered Play), a key that was never
    /// loaded throws "Unprepared asset '&lt;key&gt;'. Register it with ScenePreparation before gameplay."
    /// </summary>
    public sealed class AssetLibrary
    {
        public T Load<T>(string key) where T : UnityEngine.Object => throw new NotImplementedException();

        /// <summary>A miss is cached and returns false.</summary>
        public bool TryLoad<T>(string key, out T asset) where T : UnityEngine.Object => throw new NotImplementedException();

        public IDisposable AllowColdLoads() => throw new NotImplementedException();

        public void BeginScene() => throw new NotImplementedException();

        public void Seal() => throw new NotImplementedException();

        public bool IsSealed => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();
    }
}
