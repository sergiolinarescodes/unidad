using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class MemoryUtility
    {
        /// <summary>
        /// Add a memory. If at MaxMemories, evicts the lowest-importance memory.
        /// </summary>
        public static void AddMemory(ref DynamicBuffer<MemoryElement> memories,
            in MemoryConfig config,
            int memoryType, float3 location, float importance,
            int intParam, float floatParam, double timestamp)
        {
            if (memories.Length >= config.MaxMemories)
            {
                // Evict lowest importance
                int lowestIdx = 0;
                float lowestImportance = memories[0].Importance;
                for (int i = 1; i < memories.Length; i++)
                {
                    if (memories[i].Importance < lowestImportance)
                    {
                        lowestImportance = memories[i].Importance;
                        lowestIdx = i;
                    }
                }
                memories.RemoveAtSwapBack(lowestIdx);
            }

            memories.Add(new MemoryElement
            {
                MemoryType = memoryType,
                Location = location,
                Timestamp = timestamp,
                Importance = importance,
                IntParam = intParam,
                FloatParam = floatParam
            });
        }

        /// <summary>
        /// Find the nearest memory of a given type to a world position.
        /// Returns the index, or -1 if no match.
        /// </summary>
        public static int FindNearest(in DynamicBuffer<MemoryElement> memories,
            int memoryType, float3 position)
        {
            int bestIdx = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < memories.Length; i++)
            {
                if (memories[i].MemoryType != memoryType)
                    continue;

                float distSq = math.distancesq(memories[i].Location, position);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        /// <summary>
        /// Find the most recent memory of a given type.
        /// Returns the index, or -1 if no match.
        /// </summary>
        public static int FindMostRecent(in DynamicBuffer<MemoryElement> memories, int memoryType)
        {
            int bestIdx = -1;
            double bestTime = double.MinValue;

            for (int i = 0; i < memories.Length; i++)
            {
                if (memories[i].MemoryType != memoryType)
                    continue;

                if (memories[i].Timestamp > bestTime)
                {
                    bestTime = memories[i].Timestamp;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        /// <summary>
        /// Find the highest-importance memory of a given type.
        /// Returns the index, or -1 if no match.
        /// </summary>
        public static int FindHighestImportance(in DynamicBuffer<MemoryElement> memories, int memoryType)
        {
            int bestIdx = -1;
            float bestImportance = float.MinValue;

            for (int i = 0; i < memories.Length; i++)
            {
                if (memories[i].MemoryType != memoryType)
                    continue;

                if (memories[i].Importance > bestImportance)
                {
                    bestImportance = memories[i].Importance;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        /// <summary>
        /// Count memories of a given type.
        /// </summary>
        public static int CountByType(in DynamicBuffer<MemoryElement> memories, int memoryType)
        {
            int count = 0;
            for (int i = 0; i < memories.Length; i++)
            {
                if (memories[i].MemoryType == memoryType)
                    count++;
            }
            return count;
        }
    }
}
