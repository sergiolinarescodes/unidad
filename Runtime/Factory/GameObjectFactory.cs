using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.Factory
{
    /// <summary>
    /// Factory for creating and managing GameObjects.
    /// Tracks all created objects for cleanup.
    /// </summary>
    internal sealed class GameObjectFactory : IGameObjectFactory
    {
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private readonly HashSet<GameObject> _trackedObjects = new();
        private readonly MaterialPropertyBlock _propertyBlock = new();
        private bool _disposed;

        public GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.position = position;

            var collider = obj.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            Track(obj);
            return obj;
        }

        public GameObject CreateEmpty(string name, Transform parent = null)
        {
            var obj = new GameObject(name);
            if (parent != null)
                obj.transform.SetParent(parent, false);
            Track(obj);
            return obj;
        }

        public GameObject InstantiatePrefab(string resourcePath, string name, Vector3 position)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameObjectFactory] Prefab not found at Resources/{resourcePath}");
                return null;
            }

            var obj = Object.Instantiate(prefab, position, Quaternion.identity);
            obj.name = name;
            Track(obj);
            return obj;
        }

        public void Destroy(GameObject obj)
        {
            if (obj == null) return;
            _trackedObjects.Remove(obj);
            Object.Destroy(obj);
        }

        public void DestroyAll()
        {
            foreach (var obj in _trackedObjects)
            {
                if (obj != null)
                    Object.Destroy(obj);
            }
            _trackedObjects.Clear();
        }

        public void SetActive(GameObject obj, bool active)
        {
            if (obj != null)
                obj.SetActive(active);
        }

        public void SetColor(GameObject obj, Color color)
        {
            if (obj == null) return;
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            _propertyBlock.SetColor(ColorPropertyId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private void Track(GameObject obj) => _trackedObjects.Add(obj);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DestroyAll();
        }
    }
}
