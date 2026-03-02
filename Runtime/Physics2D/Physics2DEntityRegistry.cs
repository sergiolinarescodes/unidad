using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.Physics2D
{
    internal sealed class Physics2DEntityRegistry : IPhysics2DEntityRegistry
    {
        private struct EntityData
        {
            public GameObject GameObject;
            public string Tag;
        }

        private readonly Dictionary<int, EntityData> _entities = new();
        private readonly Dictionary<int, int> _instanceIdToEntityId = new();
        private int _nextId = 1;

        public Physics2DEntityId Register(GameObject gameObject, string tag)
        {
            var instanceId = gameObject.GetInstanceID();

            if (_instanceIdToEntityId.TryGetValue(instanceId, out var existingId))
                return new Physics2DEntityId(existingId);

            var id = _nextId++;
            _entities[id] = new EntityData { GameObject = gameObject, Tag = tag };
            _instanceIdToEntityId[instanceId] = id;
            return new Physics2DEntityId(id);
        }

        public void Unregister(Physics2DEntityId id)
        {
            if (!_entities.TryGetValue(id.Value, out var data)) return;

            if (data.GameObject != null)
                _instanceIdToEntityId.Remove(data.GameObject.GetInstanceID());

            _entities.Remove(id.Value);
        }

        public bool TryGetGameObject(Physics2DEntityId id, out GameObject gameObject)
        {
            if (_entities.TryGetValue(id.Value, out var data) && data.GameObject != null)
            {
                gameObject = data.GameObject;
                return true;
            }

            gameObject = null;
            return false;
        }

        public bool TryGetTag(Physics2DEntityId id, out string tag)
        {
            if (_entities.TryGetValue(id.Value, out var data))
            {
                tag = data.Tag;
                return true;
            }

            tag = null;
            return false;
        }

        public Physics2DEntityId GetIdForGameObject(GameObject gameObject)
        {
            if (gameObject == null) return Physics2DEntityId.None;

            var instanceId = gameObject.GetInstanceID();
            return _instanceIdToEntityId.TryGetValue(instanceId, out var entityId)
                ? new Physics2DEntityId(entityId)
                : Physics2DEntityId.None;
        }
    }
}
