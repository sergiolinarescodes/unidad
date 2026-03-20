using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class ModifierUtility
    {
        [BurstCompile]
        public static float Evaluate(in DynamicBuffer<ModifierElement> buffer, float baseValue)
        {
            if (buffer.Length == 0)
                return baseValue;

            // Copy active modifiers to temp list for sorting
            var active = new NativeList<ModifierElement>(buffer.Length, Allocator.Temp);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].IsActive)
                    active.Add(buffer[i]);
            }

            // Sort by priority descending (higher priority first)
            SortByPriorityDescending(ref active);

            float result = baseValue;
            for (int i = 0; i < active.Length; i++)
            {
                result = Apply(active[i], result);
            }

            active.Dispose();
            return result;
        }

        [BurstCompile]
        public static float EvaluateRaw(ref NativeList<ModifierElement> active, float baseValue)
        {
            float result = baseValue;
            for (int i = 0; i < active.Length; i++)
            {
                result = Apply(active[i], result);
            }
            return result;
        }

        [BurstCompile]
        public static float Apply(in ModifierElement mod, float value)
        {
            switch (mod.Op)
            {
                case ModifierOp.Add:
                    return value + mod.Value;
                case ModifierOp.Multiply:
                    return value * mod.Value;
                case ModifierOp.Override:
                    return mod.Value;
                case ModifierOp.ClampMin:
                    return value < mod.Value ? mod.Value : value;
                case ModifierOp.ClampMax:
                    return value > mod.Value ? mod.Value : value;
                default:
                    return value;
            }
        }

        [BurstCompile]
        public static bool Remove(ref DynamicBuffer<ModifierElement> buffer, int modifierId)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Id == modifierId)
                {
                    buffer.RemoveAtSwapBack(i);
                    return true;
                }
            }
            return false;
        }

        [BurstCompile]
        public static bool Has(in DynamicBuffer<ModifierElement> buffer, int modifierId)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Id == modifierId)
                    return true;
            }
            return false;
        }

        [BurstCompile]
        public static void SetActive(ref DynamicBuffer<ModifierElement> buffer, int modifierId, bool active)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Id == modifierId)
                {
                    var mod = buffer[i];
                    mod.IsActive = active;
                    buffer[i] = mod;
                    return;
                }
            }
        }

        // Simple insertion sort — modifier buffers are typically small
        static void SortByPriorityDescending(ref NativeList<ModifierElement> list)
        {
            for (int i = 1; i < list.Length; i++)
            {
                var key = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].Priority < key.Priority)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = key;
            }
        }
    }
}
