using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;
using UnityEngine;

namespace Unidad.Core.Physics2D
{
    internal sealed class Physics2DService : SystemServiceBase, IPhysics2DService
    {
        private readonly IPhysics2DEntityRegistry _registry;
        private readonly Collider2D[] _overlapBuffer = new Collider2D[64];

        public Physics2DService(IEventBus eventBus, IPhysics2DEntityRegistry registry) : base(eventBus)
        {
            _registry = registry;
        }

        public bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hit, float maxDistance)
        {
            hit = UnityEngine.Physics2D.Raycast(origin, direction, maxDistance);
            return hit.collider != null;
        }

        public bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hit, float maxDistance, int layerMask)
        {
            hit = UnityEngine.Physics2D.Raycast(origin, direction, maxDistance, layerMask);
            return hit.collider != null;
        }

        public IReadOnlyList<Collider2D> OverlapCircle(Vector2 center, float radius)
        {
            return OverlapCircleNonAlloc(center, radius, ~0);
        }

        public IReadOnlyList<Collider2D> OverlapCircle(Vector2 center, float radius, int layerMask)
        {
            return OverlapCircleNonAlloc(center, radius, layerMask);
        }

        public bool CheckCircle(Vector2 center, float radius)
        {
            return UnityEngine.Physics2D.OverlapCircle(center, radius) != null;
        }

        public Physics2DEntityId RegisterEntity(GameObject gameObject, string tag)
        {
            var id = _registry.Register(gameObject, tag);

            var reporter = gameObject.GetComponent<CollisionReporter2D>();
            if (reporter == null)
                reporter = gameObject.AddComponent<CollisionReporter2D>();

            reporter.Initialize(EventBus, _registry, id);
            return id;
        }

        public void UnregisterEntity(Physics2DEntityId id)
        {
            if (_registry.TryGetGameObject(id, out var go))
            {
                var reporter = go.GetComponent<CollisionReporter2D>();
                if (reporter != null)
                    Object.Destroy(reporter);
            }

            _registry.Unregister(id);
        }

        public Physics2DEntityId GetEntityId(GameObject gameObject)
        {
            return _registry.GetIdForGameObject(gameObject);
        }

        private List<Collider2D> OverlapCircleNonAlloc(Vector2 center, float radius, int layerMask)
        {
            var count = UnityEngine.Physics2D.OverlapCircleNonAlloc(center, radius, _overlapBuffer, layerMask);
            var result = new List<Collider2D>(count);
            for (int i = 0; i < count; i++)
                result.Add(_overlapBuffer[i]);
            return result;
        }
    }
}
