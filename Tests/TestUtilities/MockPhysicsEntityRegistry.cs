using System.Collections.Generic;
using Unidad.Core.Physics;
using UnityEngine;

namespace Unidad.Core.Tests.Tests.TestUtilities
{
    /// <summary>
    /// Simple dictionary-backed mock for IPhysicsEntityRegistry.
    /// For unit tests that need to control entity registration without real physics.
    /// </summary>
    public sealed class MockPhysicsEntityRegistry : IPhysicsEntityRegistry
    {
        private struct EntityData
        {
            public GameObject GameObject;
            public string Tag;
        }

        private readonly Dictionary<int, EntityData> _entities = new();
        private readonly Dictionary<int, int> _instanceIdToEntityId = new();
        private int _nextId = 1;

        public PhysicsEntityId Register(GameObject gameObject, string tag)
        {
            var instanceId = gameObject.GetInstanceID();
            if (_instanceIdToEntityId.TryGetValue(instanceId, out var existingId))
                return new PhysicsEntityId(existingId);

            var id = _nextId++;
            _entities[id] = new EntityData { GameObject = gameObject, Tag = tag };
            _instanceIdToEntityId[instanceId] = id;
            return new PhysicsEntityId(id);
        }

        public void Unregister(PhysicsEntityId id)
        {
            if (!_entities.TryGetValue(id.Value, out var data)) return;

            if (data.GameObject != null)
                _instanceIdToEntityId.Remove(data.GameObject.GetInstanceID());

            _entities.Remove(id.Value);
        }

        public bool TryGetGameObject(PhysicsEntityId id, out GameObject gameObject)
        {
            if (_entities.TryGetValue(id.Value, out var data) && data.GameObject != null)
            {
                gameObject = data.GameObject;
                return true;
            }

            gameObject = null;
            return false;
        }

        public bool TryGetTag(PhysicsEntityId id, out string tag)
        {
            if (_entities.TryGetValue(id.Value, out var data))
            {
                tag = data.Tag;
                return true;
            }

            tag = null;
            return false;
        }

        public PhysicsEntityId GetIdForGameObject(GameObject gameObject)
        {
            if (gameObject == null) return PhysicsEntityId.None;

            var instanceId = gameObject.GetInstanceID();
            return _instanceIdToEntityId.TryGetValue(instanceId, out var entityId)
                ? new PhysicsEntityId(entityId)
                : PhysicsEntityId.None;
        }
    }
}
