using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.Physics
{
    internal sealed class PhysicsEntityRegistry : IPhysicsEntityRegistry
    {
        private struct EntityData
        {
            public GameObject GameObject;
            public string Tag;
        }

        private readonly Dictionary<int, EntityData> _entities = new();
        private readonly Dictionary<EntityId, int> _entityIdToPhysicsId = new();
        private int _nextId = 1;

        public PhysicsEntityId Register(GameObject gameObject, string tag)
        {
            var entityId = gameObject.GetEntityId();

            if (_entityIdToPhysicsId.TryGetValue(entityId, out var existingId))
                return new PhysicsEntityId(existingId);

            var id = _nextId++;
            _entities[id] = new EntityData { GameObject = gameObject, Tag = tag };
            _entityIdToPhysicsId[entityId] = id;
            return new PhysicsEntityId(id);
        }

        public void Unregister(PhysicsEntityId id)
        {
            if (!_entities.TryGetValue(id.Value, out var data)) return;

            if (data.GameObject != null)
                _entityIdToPhysicsId.Remove(data.GameObject.GetEntityId());

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

            var entityId = gameObject.GetEntityId();
            return _entityIdToPhysicsId.TryGetValue(entityId, out var physicsId)
                ? new PhysicsEntityId(physicsId)
                : PhysicsEntityId.None;
        }
    }
}
