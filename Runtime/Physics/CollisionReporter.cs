#if UNIDAD_PHYSICS3D // optional module: define UNIDAD_PHYSICS3D in Player Settings to compile
using Unidad.Core.EventBus;
using UnityEngine;

namespace Unidad.Core.Physics
{
    /// <summary>
    /// MonoBehaviour bridge between Unity's physics callbacks and Unidad's event bus.
    /// Added to registered physics entities by PhysicsService.
    /// Skips events when the other entity is unregistered.
    /// </summary>
    internal sealed class CollisionReporter : MonoBehaviour
    {
        private IEventBus _eventBus;
        private IPhysicsEntityRegistry _registry;
        private PhysicsEntityId _entityId;

        public PhysicsEntityId EntityId => _entityId;

        public void Initialize(IEventBus eventBus, IPhysicsEntityRegistry registry, PhysicsEntityId entityId)
        {
            _eventBus = eventBus;
            _registry = registry;
            _entityId = entityId;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var otherId = ResolveOther(collision.gameObject);
            if (!otherId.IsValid) return;

            var contact = collision.GetContact(0);
            _eventBus.Publish(new CollisionBeginEvent(
                _entityId,
                otherId,
                contact.point,
                contact.normal,
                collision.relativeVelocity.magnitude));
        }

        private void OnCollisionExit(Collision collision)
        {
            var otherId = ResolveOther(collision.gameObject);
            if (!otherId.IsValid) return;

            _eventBus.Publish(new CollisionEndEvent(_entityId, otherId));
        }

        private void OnTriggerEnter(Collider other)
        {
            var otherId = ResolveOther(other.gameObject);
            if (!otherId.IsValid) return;

            _eventBus.Publish(new TriggerEnterEvent(_entityId, otherId));
        }

        private void OnTriggerExit(Collider other)
        {
            var otherId = ResolveOther(other.gameObject);
            if (!otherId.IsValid) return;

            _eventBus.Publish(new TriggerExitEvent(_entityId, otherId));
        }

        private PhysicsEntityId ResolveOther(GameObject otherGo)
        {
            // Fast path: check if the other GO has a CollisionReporter
            var otherReporter = otherGo.GetComponent<CollisionReporter>();
            if (otherReporter != null)
                return otherReporter._entityId;

            // Fallback: registry lookup
            return _registry.GetIdForGameObject(otherGo);
        }
    }
}
#endif // UNIDAD_PHYSICS3D
