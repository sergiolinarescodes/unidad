#if UNIDAD_PHYSICS3D // optional module: define UNIDAD_PHYSICS3D in Player Settings to compile
using System.Collections.Generic;
using Unidad.Core.Physics;
using UnityEngine;

namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Abstraction over Unity's Physics system.
    /// Production: delegates to UnityEngine.Physics.
    /// Tests: returns configurable mock results.
    /// For games needing real physics in tests, use [UnityTest] Play Mode tests.
    /// </summary>
    public interface IPhysicsService
    {
        // --- Query methods ---

        bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance);
        bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance, int layerMask);
        IReadOnlyList<Collider> OverlapSphere(Vector3 center, float radius);
        IReadOnlyList<Collider> OverlapSphere(Vector3 center, float radius, int layerMask);
        bool CheckSphere(Vector3 center, float radius);

        // --- Entity registration ---

        /// <summary>
        /// Register a GameObject as a physics entity. Adds a CollisionReporter component
        /// so collision/trigger events are published to the event bus.
        /// </summary>
        PhysicsEntityId RegisterEntity(GameObject gameObject, string tag);

        /// <summary>
        /// Unregister a physics entity and remove its CollisionReporter component.
        /// </summary>
        void UnregisterEntity(PhysicsEntityId id);

        /// <summary>
        /// Get the PhysicsEntityId for a registered GameObject.
        /// Returns PhysicsEntityId.None if not registered.
        /// </summary>
        PhysicsEntityId GetEntityId(GameObject gameObject);
    }
}
#endif // UNIDAD_PHYSICS3D
