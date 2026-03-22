using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class NeedUtility
    {
        public static NeedUrgency EvaluateUrgency(
            float currentValue, float criticalThreshold, float lowThreshold, float highThreshold)
        {
            if (currentValue <= criticalThreshold)
                return NeedUrgency.Critical;
            if (currentValue <= lowThreshold)
                return NeedUrgency.Low;
            if (currentValue >= highThreshold)
                return NeedUrgency.Satisfied;
            return NeedUrgency.Normal;
        }

        public static float GetEffectiveDecayRate(
            int resourceId, float baseDecayRate,
            in DynamicBuffer<NeedDecayModifier> decayMods)
        {
            var active = new FixedList128Bytes<ModifierElement>();
            for (int i = 0; i < decayMods.Length; i++)
            {
                if (decayMods[i].ResourceId == resourceId && decayMods[i].Modifier.IsActive)
                    active.Add(decayMods[i].Modifier);
            }
            return ModifierUtility.EvaluateSorted(ref active, baseDecayRate);
        }

        /// <summary>
        /// Returns the normalized deficit (0..1) where 0 = fully satisfied, 1 = fully depleted.
        /// Used as input to the scoring system.
        /// </summary>
        public static float GetNormalizedDeficit(
            float currentValue, float effectiveMin, float effectiveMax)
        {
            if (effectiveMax <= effectiveMin)
                return 0f;
            return 1f - ((currentValue - effectiveMin) / (effectiveMax - effectiveMin));
        }

        public static int FindNeed(in DynamicBuffer<NeedElement> needs, int resourceId)
        {
            for (int i = 0; i < needs.Length; i++)
            {
                if (needs[i].ResourceId == resourceId)
                    return i;
            }
            return -1;
        }
    }
}
