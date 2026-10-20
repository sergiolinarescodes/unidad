using System;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>The one place GameObjects are created, instantiated and destroyed; every object it made dies with the scene.</summary>
    public interface IGameObjectFactory : IDisposable
    {
        /// <summary>"[Objects]", a child of the bootstrap.</summary>
        Transform Root { get; }

        GameObject Create(string name, Transform parent = null);

        T Create<T>(string name, Transform parent = null) where T : Component;

        /// <summary>A prefab through the AssetLibrary.</summary>
        GameObject Instantiate(string assetKey, string name, Transform parent = null);

        /// <summary>A catalogue model, scaled, with every model decorator applied in order.</summary>
        GameObject InstantiateModel(string modelId, string name, Transform parent = null);

        void Destroy(GameObject gameObject);

        void DestroyAll();

        void SetActive(GameObject gameObject, bool active);
    }
}
