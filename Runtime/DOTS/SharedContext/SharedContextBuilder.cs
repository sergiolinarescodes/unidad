using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Fluent builder for creating shared context definition entities.
    /// </summary>
    public struct SharedContextBuilder
    {
        EntityManager _em;
        int _scopeId, _archetypeId;
        List<SharedContextEntry> _entries;
        List<ContextAccessRule> _rules;

        static int _nextScopeId;

        public static SharedContextBuilder Create(EntityManager em)
        {
            return new SharedContextBuilder
            {
                _em = em,
                _archetypeId = -1,
                _entries = new List<SharedContextEntry>(16),
                _rules = new List<ContextAccessRule>(8)
            };
        }

        public SharedContextBuilder AsGlobal()
        {
            _archetypeId = -1;
            _scopeId = 0;
            return this;
        }

        public SharedContextBuilder ForArchetype(int archetypeId)
        {
            _archetypeId = archetypeId;
            _scopeId = ++_nextScopeId;
            return this;
        }

        public SharedContextBuilder WithEntry(int key, float value)
        {
            _entries.Add(new SharedContextEntry { Key = key, Value = value });
            return this;
        }

        public SharedContextBuilder WithAccessRule(int archetypeId, int key, ContextAccessLevel access)
        {
            _rules.Add(new ContextAccessRule
            {
                ArchetypeId = archetypeId,
                Key = key,
                Access = access
            });
            return this;
        }

        public Entity Build()
        {
            var types = new NativeList<ComponentType>(8, Allocator.Temp);
            types.Add(ComponentType.ReadWrite<SharedContextData>());
            types.Add(ComponentType.ReadWrite<SharedContextEntry>());
            types.Add(ComponentType.ReadWrite<ContextAccessRule>());

            var archetype = _em.CreateArchetype(types.AsArray());
            var entity = _em.CreateEntity(archetype);
            types.Dispose();

            _em.SetComponentData(entity, new SharedContextData
            {
                ScopeId = _scopeId,
                ArchetypeId = _archetypeId
            });

            var entryBuf = _em.GetBuffer<SharedContextEntry>(entity);
            for (int i = 0; i < _entries.Count; i++)
                entryBuf.Add(_entries[i]);

            var ruleBuf = _em.GetBuffer<ContextAccessRule>(entity);
            for (int i = 0; i < _rules.Count; i++)
                ruleBuf.Add(_rules[i]);

            return entity;
        }
    }
}
