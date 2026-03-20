using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class ResourceUtility
    {
        [BurstCompile]
        public static float GetEffectiveMax(int resourceId, float baseMax,
            in DynamicBuffer<ResourceMaxModifier> modBuffer)
        {
            var active = new NativeList<ModifierElement>(modBuffer.Length, Allocator.Temp);
            for (int i = 0; i < modBuffer.Length; i++)
            {
                if (modBuffer[i].ResourceId == resourceId && modBuffer[i].Modifier.IsActive)
                    active.Add(modBuffer[i].Modifier);
            }

            float result = ModifierUtility.EvaluateRaw(ref active, baseMax);
            active.Dispose();
            return result;
        }

        [BurstCompile]
        public static float GetEffectiveMin(int resourceId, float baseMin,
            in DynamicBuffer<ResourceMinModifier> modBuffer)
        {
            var active = new NativeList<ModifierElement>(modBuffer.Length, Allocator.Temp);
            for (int i = 0; i < modBuffer.Length; i++)
            {
                if (modBuffer[i].ResourceId == resourceId && modBuffer[i].Modifier.IsActive)
                    active.Add(modBuffer[i].Modifier);
            }

            float result = ModifierUtility.EvaluateRaw(ref active, baseMin);
            active.Dispose();
            return result;
        }

        [BurstCompile]
        public static void Set(
            ref DynamicBuffer<ResourceElement> resources,
            ref DynamicBuffer<ResourceChangeRecord> changes,
            in DynamicBuffer<ResourceMaxModifier> maxMods,
            in DynamicBuffer<ResourceMinModifier> minMods,
            int resourceId, float newValue)
        {
            for (int i = 0; i < resources.Length; i++)
            {
                var r = resources[i];
                if (r.ResourceId != resourceId)
                    continue;

                float effMax = GetEffectiveMax(resourceId, r.BaseMax, in maxMods);
                float effMin = GetEffectiveMin(resourceId, r.BaseMin, in minMods);
                float oldValue = r.CurrentValue;
                r.CurrentValue = math.clamp(newValue, effMin, effMax);
                resources[i] = r;

                if (!ApproxEqual(oldValue, r.CurrentValue))
                {
                    changes.Add(new ResourceChangeRecord
                    {
                        ResourceId = resourceId,
                        OldValue = oldValue,
                        NewValue = r.CurrentValue,
                        EffectiveMax = effMax
                    });
                }
                return;
            }
        }

        [BurstCompile]
        public static void Add(
            ref DynamicBuffer<ResourceElement> resources,
            ref DynamicBuffer<ResourceChangeRecord> changes,
            in DynamicBuffer<ResourceMaxModifier> maxMods,
            in DynamicBuffer<ResourceMinModifier> minMods,
            int resourceId, float amount)
        {
            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i].ResourceId == resourceId)
                {
                    Set(ref resources, ref changes, in maxMods, in minMods,
                        resourceId, resources[i].CurrentValue + amount);
                    return;
                }
            }
        }

        [BurstCompile]
        public static bool TrySpend(
            ref DynamicBuffer<ResourceElement> resources,
            ref DynamicBuffer<ResourceChangeRecord> changes,
            in DynamicBuffer<ResourceMaxModifier> maxMods,
            in DynamicBuffer<ResourceMinModifier> minMods,
            int resourceId, float amount)
        {
            for (int i = 0; i < resources.Length; i++)
            {
                var r = resources[i];
                if (r.ResourceId != resourceId)
                    continue;

                float effMin = GetEffectiveMin(resourceId, r.BaseMin, in minMods);
                if (r.CurrentValue - amount < effMin)
                    return false;

                Set(ref resources, ref changes, in maxMods, in minMods,
                    resourceId, r.CurrentValue - amount);
                return true;
            }
            return false;
        }

        [BurstCompile]
        public static float Get(in DynamicBuffer<ResourceElement> resources, int resourceId)
        {
            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i].ResourceId == resourceId)
                    return resources[i].CurrentValue;
            }
            return 0f;
        }

        [BurstCompile]
        static bool ApproxEqual(float a, float b)
        {
            return math.abs(a - b) < 0.0001f;
        }
    }
}
