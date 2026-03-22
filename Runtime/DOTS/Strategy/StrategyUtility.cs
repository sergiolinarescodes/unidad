using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class StrategyUtility
    {
        public static float GetParam(in DynamicBuffer<StrategyParamElement> strategyParams, int paramId)
        {
            for (int i = 0; i < strategyParams.Length; i++)
            {
                if (strategyParams[i].ParamId == paramId)
                    return strategyParams[i].Value;
            }
            return 0f;
        }

        public static void SetParam(
            ref DynamicBuffer<StrategyParamElement> strategyParams, int paramId, float value)
        {
            for (int i = 0; i < strategyParams.Length; i++)
            {
                if (strategyParams[i].ParamId == paramId)
                {
                    var p = strategyParams[i];
                    p.Value = value;
                    strategyParams[i] = p;
                    return;
                }
            }
            strategyParams.Add(new StrategyParamElement { ParamId = paramId, Value = value });
        }

        /// <summary>
        /// Checks if all bits in requiredFlags are set in availableFlags.
        /// </summary>
        public static bool CheckPreconditions(int requiredFlags, int availableFlags)
        {
            return (availableFlags & requiredFlags) == requiredFlags;
        }

        /// <summary>
        /// Find the strategy definition entity by StrategyId from pre-gathered arrays.
        /// </summary>
        public static Entity FindStrategyEntity(
            in NativeArray<Entity> strategyEntities,
            in NativeArray<StrategyDefinition> strategyDatas,
            int strategyId)
        {
            for (int i = 0; i < strategyDatas.Length; i++)
            {
                if (strategyDatas[i].StrategyId == strategyId)
                    return strategyEntities[i];
            }
            return Entity.Null;
        }

        /// <summary>
        /// Build a HashMap for O(1) strategy lookup. Call once per system OnUpdate,
        /// dispose at end. Preferred over linear FindStrategyEntity for hot paths.
        /// </summary>
        public static NativeHashMap<int, Entity> BuildStrategyLookup(
            in NativeArray<Entity> strategyEntities,
            in NativeArray<StrategyDefinition> strategyDatas,
            Allocator allocator)
        {
            var map = new NativeHashMap<int, Entity>(
                math.max(strategyDatas.Length * 2, 4), allocator);
            for (int i = 0; i < strategyDatas.Length; i++)
                map.TryAdd(strategyDatas[i].StrategyId, strategyEntities[i]);
            return map;
        }

        /// <summary>
        /// O(1) strategy entity lookup via pre-built HashMap.
        /// </summary>
        public static Entity FindStrategyEntity(
            in NativeHashMap<int, Entity> lookup, int strategyId)
        {
            return lookup.TryGetValue(strategyId, out var entity) ? entity : Entity.Null;
        }
    }
}
