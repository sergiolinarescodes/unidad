using System.Collections.Generic;
using UnityEngine;
using Unidad.Core.Factory;

namespace Unidad.Core.ModelCatalog
{
    /// <summary>
    /// Minimal IGameObjectFactory for tests and scenarios — the framework's
    /// GameObjectFactory is internal to Unidad.Core.Runtime, and production code
    /// resolves the container-registered instance instead of this.
    /// Edit-mode aware: destroys immediately outside play mode.
    /// </summary>
    internal sealed class LocalGameObjectFactory : IGameObjectFactory
    {
        readonly HashSet<GameObject> _tracked = new();

        public GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.position = position;
            _tracked.Add(obj);
            return obj;
        }

        public GameObject CreateEmpty(string name, Transform parent = null)
        {
            var obj = new GameObject(name);
            if (parent != null)
                obj.transform.SetParent(parent, false);
            _tracked.Add(obj);
            return obj;
        }

        public GameObject InstantiatePrefab(string resourcePath, string name, Vector3 position)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
                return null;
            var obj = Object.Instantiate(prefab, position, Quaternion.identity);
            obj.name = name;
            _tracked.Add(obj);
            return obj;
        }

        public void Destroy(GameObject obj)
        {
            if (obj == null) return;
            _tracked.Remove(obj);
            DestroyObject(obj);
        }

        public void DestroyAll()
        {
            foreach (var obj in _tracked)
            {
                if (obj != null)
                    DestroyObject(obj);
            }
            _tracked.Clear();
        }

        public void SetActive(GameObject obj, bool active)
        {
            if (obj != null) obj.SetActive(active);
        }

        public void SetColor(GameObject obj, Color color)
        {
            var renderer = obj != null ? obj.GetComponentInChildren<Renderer>() : null;
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }

        public void Dispose() => DestroyAll();

        static void DestroyObject(GameObject obj)
        {
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}
