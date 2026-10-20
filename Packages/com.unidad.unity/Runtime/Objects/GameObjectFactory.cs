using System;
using System.Collections.Generic;
using Unidad.Adapter.Models;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unidad.Adapter
{
    /// <summary>Creates and tracks GameObjects under "[Objects]"; destroys every live one on dispose (10's NodeFactory).</summary>
    internal sealed class GameObjectFactory : IGameObjectFactory
    {
        // Objects destroyed without Destroy (their own, a destroyed parent) are swept once the set doubles.
        private const int MinSweepCount = 256;

        private readonly HashSet<GameObject> _tracked = new();
        private readonly AssetLibrary _assets;
        private readonly Func<IModelCatalog> _catalog;
        private readonly Func<IReadOnlyList<IModelDecorator>> _decorators;
        private int _sweepAt = MinSweepCount;
        private bool _disposed;

        public GameObjectFactory(Transform root, AssetLibrary assets, Func<IModelCatalog> catalog,
            Func<IReadOnlyList<IModelDecorator>> decorators)
        {
            Root = root ? root : throw new ArgumentNullException(nameof(root));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _decorators = decorators ?? throw new ArgumentNullException(nameof(decorators));
        }

        public Transform Root { get; }

        public GameObject Create(string name, Transform parent = null)
        {
            var created = new GameObject(name);
            Attach(created, parent, false);
            return created;
        }

        public T Create<T>(string name, Transform parent = null) where T : Component => Create(name, parent).AddComponent<T>();

        public GameObject Instantiate(string assetKey, string name, Transform parent = null)
        {
            var created = Object.Instantiate(_assets.Load<GameObject>(assetKey), parent ? parent : Root, false);
            created.name = name;
            Attach(created, null, true);
            return created;
        }

        public GameObject InstantiateModel(string modelId, string name, Transform parent = null)
        {
            var entry = _catalog().Get(modelId);
            var created = Instantiate(entry.AssetKey, name, parent);
            created.transform.localScale *= entry.Scale;
            foreach (var decorator in _decorators()) decorator.Decorate(created, entry);
            return created;
        }

        public void Destroy(GameObject gameObject)
        {
            if (gameObject is null || !gameObject) return;
            if (!_tracked.Remove(gameObject)) throw new ArgumentException($"'{gameObject.name}' was not created by this factory.", nameof(gameObject));
            Object.Destroy(gameObject);
        }

        public void DestroyAll()
        {
            foreach (var tracked in _tracked)
                if (tracked) Object.Destroy(tracked);
            _tracked.Clear();
            _sweepAt = MinSweepCount;
        }

        public void SetActive(GameObject gameObject, bool active)
        {
            if (gameObject) gameObject.SetActive(active);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DestroyAll();
        }

        private void Attach(GameObject created, Transform parent, bool parented)
        {
            if (!parented) created.transform.SetParent(parent ? parent : Root, false);
            _tracked.Add(created);
            if (_tracked.Count < _sweepAt) return;
            _tracked.RemoveWhere(tracked => !tracked);
            _sweepAt = Math.Max(MinSweepCount, _tracked.Count * 2);
        }
    }
}
