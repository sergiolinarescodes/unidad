using UnityEngine;

namespace Unidad.Core.Physics
{
    /// <summary>
    /// Registry that maps GameObjects to PhysicsEntityIds.
    /// Collision events carry IDs, not references — game code looks up objects here when needed.
    /// </summary>
    public interface IPhysicsEntityRegistry
    {
        /// <summary>
        /// Register a GameObject and assign it a PhysicsEntityId.
        /// Idempotent: re-registering the same GO returns the existing ID.
        /// </summary>
        PhysicsEntityId Register(GameObject gameObject, string tag);

        /// <summary>
        /// Unregister an entity by its ID.
        /// </summary>
        void Unregister(PhysicsEntityId id);

        /// <summary>
        /// Try to get the GameObject for a given entity ID.
        /// Returns false if the entity is not registered or has been destroyed.
        /// </summary>
        bool TryGetGameObject(PhysicsEntityId id, out GameObject gameObject);

        /// <summary>
        /// Try to get the tag for a given entity ID.
        /// </summary>
        bool TryGetTag(PhysicsEntityId id, out string tag);

        /// <summary>
        /// Get the PhysicsEntityId for a GameObject.
        /// Returns PhysicsEntityId.None if the GameObject is not registered.
        /// </summary>
        PhysicsEntityId GetIdForGameObject(GameObject gameObject);
    }
}
