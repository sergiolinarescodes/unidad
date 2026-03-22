using Unity.Burst;
using Unity.Entities;

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
            in Unity.Collections.NativeArray<Entity> strategyEntities,
            in Unity.Collections.NativeArray<StrategyDefinition> strategyDatas,
            int strategyId)
        {
            for (int i = 0; i < strategyDatas.Length; i++)
            {
                if (strategyDatas[i].StrategyId == strategyId)
                    return strategyEntities[i];
            }
            return Entity.Null;
        }
    }
}
