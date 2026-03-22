using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class SharedContextUtility
    {
        /// <summary>
        /// Read a value from the global broadcast array by key.
        /// Returns 0f if key is out of range.
        /// </summary>
        public static float Get(in NativeArray<float> broadcast, int key)
        {
            if (key < 0 || key >= broadcast.Length)
                return 0f;
            return broadcast[key];
        }

        /// <summary>
        /// Read a value from a per-agent context snapshot by key.
        /// Returns 0f if key is not in the snapshot.
        /// </summary>
        public static float GetFromSnapshot(in DynamicBuffer<AgentContextSnapshot> snapshot, int key)
        {
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i].Key == key)
                    return snapshot[i].Value;
            }
            return 0f;
        }

        /// <summary>
        /// Set a value on a shared context entity's entry buffer.
        /// Updates existing key or adds a new entry.
        /// </summary>
        public static void Set(ref DynamicBuffer<SharedContextEntry> entries, int key, float value,
            double currentTime)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Key == key)
                {
                    var entry = entries[i];
                    entry.Value = value;
                    entry.LastUpdatedTime = currentTime;
                    entries[i] = entry;
                    return;
                }
            }
            entries.Add(new SharedContextEntry
            {
                Key = key,
                Value = value,
                LastUpdatedTime = currentTime
            });
        }

        /// <summary>
        /// Check if an archetype has read access to a specific key.
        /// </summary>
        public static bool HasAccess(in DynamicBuffer<ContextAccessRule> rules,
            int archetypeId, int key)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i].ArchetypeId == archetypeId && rules[i].Key == key
                    && rules[i].Access >= ContextAccessLevel.Read)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Find the context entity for a given scope. Returns Entity.Null if not found.
        /// </summary>
        public static Entity FindContextEntity(
            in NativeArray<Entity> contextEntities,
            in NativeArray<SharedContextData> contextDatas,
            int archetypeId)
        {
            for (int i = 0; i < contextDatas.Length; i++)
            {
                if (contextDatas[i].ArchetypeId == archetypeId)
                    return contextEntities[i];
            }
            return Entity.Null;
        }
    }
}
