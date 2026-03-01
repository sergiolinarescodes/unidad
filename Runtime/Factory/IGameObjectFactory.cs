using System;
using UnityEngine;

namespace Unidad.Core.Factory
{
    /// <summary>
    /// Factory for creating and managing GameObjects.
    /// All created GameObjects are tracked for cleanup.
    /// Simplified from AutoRoguePG: only essential creation methods.
    /// </summary>
    public interface IGameObjectFactory : IDisposable
    {
        /// <summary>Creates a primitive mesh (quad, cube, etc.).</summary>
        GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position);

        /// <summary>Creates an empty GameObject as a container.</summary>
        GameObject CreateEmpty(string name, Transform parent = null);

        /// <summary>Instantiates a prefab from Resources folder.</summary>
        GameObject InstantiatePrefab(string resourcePath, string name, Vector3 position);

        /// <summary>Destroys a tracked GameObject.</summary>
        void Destroy(GameObject obj);

        /// <summary>Destroys all GameObjects created by this factory.</summary>
        void DestroyAll();

        /// <summary>Sets the active state of a GameObject.</summary>
        void SetActive(GameObject obj, bool active);

        /// <summary>Sets material color on a GameObject's renderer.</summary>
        void SetColor(GameObject obj, Color color);
    }
}
