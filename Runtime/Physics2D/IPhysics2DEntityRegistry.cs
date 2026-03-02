using UnityEngine;

namespace Unidad.Core.Physics2D
{
    /// <summary>
    /// Registry that maps GameObjects to Physics2DEntityIds.
    /// </summary>
    public interface IPhysics2DEntityRegistry
    {
        Physics2DEntityId Register(GameObject gameObject, string tag);
        void Unregister(Physics2DEntityId id);
        bool TryGetGameObject(Physics2DEntityId id, out GameObject gameObject);
        bool TryGetTag(Physics2DEntityId id, out string tag);
        Physics2DEntityId GetIdForGameObject(GameObject gameObject);
    }
}
