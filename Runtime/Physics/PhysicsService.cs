#if UNIDAD_PHYSICS3D // optional module: define UNIDAD_PHYSICS3D in Player Settings to compile
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;
using UnityEngine;

namespace Unidad.Core.Physics
{
    internal sealed class PhysicsService : SystemServiceBase, IPhysicsService
    {
        private readonly IPhysicsEntityRegistry _registry;
        private readonly Collider[] _overlapBuffer = new Collider[64];

        public PhysicsService(IEventBus eventBus, IPhysicsEntityRegistry registry) : base(eventBus)
        {
            _registry = registry;
        }

        // --- Query methods ---

        public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance)
        {
            return UnityEngine.Physics.Raycast(origin, direction, out hit, maxDistance);
        }

        public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance, int layerMask)
        {
            return UnityEngine.Physics.Raycast(origin, direction, out hit, maxDistance, layerMask);
        }

        public IReadOnlyList<Collider> OverlapSphere(Vector3 center, float radius)
        {
            return OverlapSphereNonAlloc(center, radius, ~0);
        }

        public IReadOnlyList<Collider> OverlapSphere(Vector3 center, float radius, int layerMask)
        {
            return OverlapSphereNonAlloc(center, radius, layerMask);
        }

        public bool CheckSphere(Vector3 center, float radius)
        {
            return UnityEngine.Physics.CheckSphere(center, radius);
        }

        // --- Entity registration ---

        public PhysicsEntityId RegisterEntity(GameObject gameObject, string tag)
        {
            var id = _registry.Register(gameObject, tag);

            var reporter = gameObject.GetComponent<CollisionReporter>();
            if (reporter == null)
                reporter = gameObject.AddComponent<CollisionReporter>();

            reporter.Initialize(EventBus, _registry, id);
            return id;
        }

        public void UnregisterEntity(PhysicsEntityId id)
        {
            if (_registry.TryGetGameObject(id, out var go))
            {
                var reporter = go.GetComponent<CollisionReporter>();
                if (reporter != null)
                    Object.Destroy(reporter);
            }

            _registry.Unregister(id);
        }

        public PhysicsEntityId GetEntityId(GameObject gameObject)
        {
            return _registry.GetIdForGameObject(gameObject);
        }

        private List<Collider> OverlapSphereNonAlloc(Vector3 center, float radius, int layerMask)
        {
            var count = UnityEngine.Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer, layerMask);
            var result = new List<Collider>(count);
            for (int i = 0; i < count; i++)
                result.Add(_overlapBuffer[i]);
            return result;
        }
    }
}
#endif // UNIDAD_PHYSICS3D
