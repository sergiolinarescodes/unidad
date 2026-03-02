using Unidad.Core.EventBus;
using UnityEngine;

namespace Unidad.Core.Physics2D
{
    /// <summary>
    /// MonoBehaviour bridge between Unity's 2D physics callbacks and Unidad's event bus.
    /// Added to registered 2D physics entities by Physics2DService.
    /// </summary>
    internal sealed class CollisionReporter2D : MonoBehaviour
    {
        private IEventBus _eventBus;
        private IPhysics2DEntityRegistry _registry;
        private Physics2DEntityId _entityId;

        public Physics2DEntityId EntityId => _entityId;

        public void Initialize(IEventBus eventBus, IPhysics2DEntityRegistry registry, Physics2DEntityId entityId)
        {
            _eventBus = eventBus;
            _registry = registry;
            _entityId = entityId;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            var otherId = ResolveOther(collision.gameObject);
            if (!otherId.IsValid) return;

            var contact = collision.GetContact(0);
            _eventBus.Publish(new Collision2DBeginEvent(
                _entityId,
                otherId,
                contact.point,
                contact.normal,
                collision.relativeVelocity.magnitude));
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            var otherId = ResolveOther(collision.gameObject);
            if (!otherId.IsValid) return;

            _eventBus.Publish(new Collision2DEndEvent(_entityId, otherId));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var otherId = ResolveOther(other.gameObject);
            if (!otherId.IsValid) return;

            _eventBus.Publish(new Trigger2DEnterEvent(_entityId, otherId));
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var otherId = ResolveOther(other.gameObject);
            if (!otherId.IsValid) return;

            _eventBus.Publish(new Trigger2DExitEvent(_entityId, otherId));
        }

        private Physics2DEntityId ResolveOther(GameObject otherGo)
        {
            var otherReporter = otherGo.GetComponent<CollisionReporter2D>();
            if (otherReporter != null)
                return otherReporter._entityId;

            return _registry.GetIdForGameObject(otherGo);
        }
    }
}
