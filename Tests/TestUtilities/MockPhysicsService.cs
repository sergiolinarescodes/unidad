#if UNIDAD_PHYSICS3D // optional module: define UNIDAD_PHYSICS3D in Player Settings to compile
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.Physics;
using UnityEngine;

namespace Unidad.Core.Tests.Tests.TestUtilities
{
    /// <summary>
    /// Mock physics service for unit tests.
    /// Provides configurable raycast/overlap results and manual entity ID assignment.
    /// </summary>
    public sealed class MockPhysicsService : IPhysicsService
    {
        public bool RaycastResult { get; set; }
        public RaycastHit RaycastHitResult { get; set; }
        public List<Collider> OverlapSphereResult { get; set; } = new();
        public bool CheckSphereResult { get; set; }

        private int _nextEntityId = 1;
        private readonly Dictionary<EntityId, PhysicsEntityId> _registeredEntities = new();

        public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance)
        {
            hit = RaycastHitResult;
            return RaycastResult;
        }

        public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance, int layerMask)
        {
            hit = RaycastHitResult;
            return RaycastResult;
        }

        public IReadOnlyList<Collider> OverlapSphere(Vector3 center, float radius)
        {
            return OverlapSphereResult;
        }

        public IReadOnlyList<Collider> OverlapSphere(Vector3 center, float radius, int layerMask)
        {
            return OverlapSphereResult;
        }

        public bool CheckSphere(Vector3 center, float radius)
        {
            return CheckSphereResult;
        }

        public PhysicsEntityId RegisterEntity(GameObject gameObject, string tag)
        {
            var entityId = gameObject.GetEntityId();
            if (_registeredEntities.TryGetValue(entityId, out var existing))
                return existing;

            var id = new PhysicsEntityId(_nextEntityId++);
            _registeredEntities[entityId] = id;
            return id;
        }

        public void UnregisterEntity(PhysicsEntityId id)
        {
            EntityId toRemove = EntityId.None;
            foreach (var kvp in _registeredEntities)
            {
                if (kvp.Value == id)
                {
                    toRemove = kvp.Key;
                    break;
                }
            }
            if (toRemove.IsValid())
                _registeredEntities.Remove(toRemove);
        }

        public PhysicsEntityId GetEntityId(GameObject gameObject)
        {
            var entityId = gameObject.GetEntityId();
            return _registeredEntities.TryGetValue(entityId, out var id) ? id : PhysicsEntityId.None;
        }
    }
}
#endif // UNIDAD_PHYSICS3D
