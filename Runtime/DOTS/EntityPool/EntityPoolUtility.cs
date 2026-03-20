using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class EntityPoolUtility
    {
        /// <summary>
        /// Deterministic Burst-compatible hash for pool names.
        /// </summary>
        [BurstCompile]
        public static int HashPoolId(in FixedString64Bytes name)
        {
            return name.GetHashCode();
        }

        /// <summary>
        /// Count available (disabled) pooled entities for a given pool.
        /// Must be called on the main thread with a query matching
        /// Pooled + Disabled entities.
        /// </summary>
        public static int GetAvailableCount(in NativeArray<Pooled> pooledArray, int poolId)
        {
            int count = 0;
            for (int i = 0; i < pooledArray.Length; i++)
            {
                if (pooledArray[i].PoolId == poolId)
                    count++;
            }
            return count;
        }
    }
}
