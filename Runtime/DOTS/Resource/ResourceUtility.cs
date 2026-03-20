using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class ResourceUtility
    {
        public static float GetEffectiveMax(int resourceId, float baseMax,
            in DynamicBuffer<ResourceMaxModifier> modBuffer)
        {
            var active = new NativeList<ModifierElement>(modBuffer.Length, Allocator.Temp);
            for (int i = 0; i < modBuffer.Length; i++)
            {
                if (modBuffer[i].ResourceId == resourceId && modBuffer[i].Modifier.IsActive)
                    active.Add(modBuffer[i].Modifier);
            }

            float result = ModifierUtility.EvaluateSorted(ref active, baseMax);
            active.Dispose();
            return result;
        }

        public static float GetEffectiveMin(int resourceId, float baseMin,
            in DynamicBuffer<ResourceMinModifier> modBuffer)
        {
            var active = new NativeList<ModifierElement>(modBuffer.Length, Allocator.Temp);
            for (int i = 0; i < modBuffer.Length; i++)
            {
                if (modBuffer[i].ResourceId == resourceId && modBuffer[i].Modifier.IsActive)
                    active.Add(modBuffer[i].Modifier);
            }

            float result = ModifierUtility.EvaluateSorted(ref active, baseMin);
            active.Dispose();
            return result;
        }

        public static void Set(
            ref DynamicBuffer<ResourceElement> resources,
            ref DynamicBuffer<ResourceChangeRecord> changes,
            in DynamicBuffer<ResourceMaxModifier> maxMods,
            in DynamicBuffer<ResourceMinModifier> minMods,
            int resourceId, float newValue)
        {
            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i].ResourceId == resourceId)
                {
                    SetAtIndex(ref resources, ref changes, in maxMods, in minMods, i, newValue);
                    return;
                }
            }
        }

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
                    SetAtIndex(ref resources, ref changes, in maxMods, in minMods,
                        i, resources[i].CurrentValue + amount);
                    return;
                }
            }
        }

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

                SetAtIndex(ref resources, ref changes, in maxMods, in minMods,
                    i, r.CurrentValue - amount);
                return true;
            }
            return false;
        }

        public static float Get(in DynamicBuffer<ResourceElement> resources, int resourceId)
        {
            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i].ResourceId == resourceId)
                    return resources[i].CurrentValue;
            }
            return 0f;
        }

        static void SetAtIndex(
            ref DynamicBuffer<ResourceElement> resources,
            ref DynamicBuffer<ResourceChangeRecord> changes,
            in DynamicBuffer<ResourceMaxModifier> maxMods,
            in DynamicBuffer<ResourceMinModifier> minMods,
            int index, float newValue)
        {
            var r = resources[index];
            float effMax = GetEffectiveMax(r.ResourceId, r.BaseMax, in maxMods);
            float effMin = GetEffectiveMin(r.ResourceId, r.BaseMin, in minMods);
            float oldValue = r.CurrentValue;
            r.CurrentValue = math.clamp(newValue, effMin, effMax);
            resources[index] = r;

            if (math.abs(oldValue - r.CurrentValue) > 0.0001f)
            {
                changes.Add(new ResourceChangeRecord
                {
                    ResourceId = r.ResourceId,
                    OldValue = oldValue,
                    NewValue = r.CurrentValue,
                    EffectiveMax = effMax,
                    EffectiveMin = effMin
                });
            }
        }
    }
}
