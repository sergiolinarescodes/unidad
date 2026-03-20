using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class InventoryUtility
    {
        public static int GetEffectiveCapacity(in InventoryData data,
            in DynamicBuffer<InventoryCapacityModifier> capMods)
        {
            if (capMods.Length == 0)
                return data.BaseSlotCount;

            var active = new NativeList<ModifierElement>(capMods.Length, Allocator.Temp);
            for (int i = 0; i < capMods.Length; i++)
            {
                if (capMods[i].Modifier.IsActive)
                    active.Add(capMods[i].Modifier);
            }

            float result = ModifierUtility.EvaluateSorted(ref active, data.BaseSlotCount);
            active.Dispose();

            int capacity = (int)math.round(result);
            return math.clamp(capacity, 0, data.MaxSlotCount);
        }

        /// <summary>
        /// Adds items using 2-pass approach: fill partial stacks first, then empty slots.
        /// Returns the overflow count (items that could not be added).
        /// </summary>
        public static int Add(
            ref DynamicBuffer<InventorySlotElement> slots,
            in InventoryData data,
            in DynamicBuffer<InventoryCapacityModifier> capMods,
            int itemId, int maxStackSize, int count)
        {
            int remaining = count;
            int effectiveCap = GetEffectiveCapacity(in data, in capMods);

            // Pass 1: fill existing partial stacks
            for (int i = 0; i < slots.Length && i < effectiveCap && remaining > 0; i++)
            {
                var slot = slots[i];
                if (slot.ItemId != itemId || slot.Count >= maxStackSize)
                    continue;

                int space = maxStackSize - slot.Count;
                int toAdd = math.min(space, remaining);
                slot.Count += toAdd;
                remaining -= toAdd;
                slots[i] = slot;
            }

            // Pass 2: fill empty slots
            for (int i = 0; i < slots.Length && i < effectiveCap && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty)
                    continue;

                int toAdd = math.min(maxStackSize, remaining);
                slots[i] = new InventorySlotElement { ItemId = itemId, Count = toAdd };
                remaining -= toAdd;
            }

            return remaining;
        }

        /// <summary>
        /// Removes items back-to-front. Returns true if the full amount was removed.
        /// </summary>
        public static bool TryRemove(
            ref DynamicBuffer<InventorySlotElement> slots,
            in InventoryData data,
            in DynamicBuffer<InventoryCapacityModifier> capMods,
            int itemId, int count)
        {
            int effectiveCap = GetEffectiveCapacity(in data, in capMods);

            int total = 0;
            int limit = math.min(slots.Length, effectiveCap);
            for (int i = 0; i < limit; i++)
            {
                if (slots[i].ItemId == itemId)
                    total += slots[i].Count;
            }

            if (total < count)
                return false;

            int remaining = count;
            for (int i = limit - 1; i >= 0 && remaining > 0; i--)
            {
                var slot = slots[i];
                if (slot.ItemId != itemId)
                    continue;

                int toRemove = math.min(slot.Count, remaining);
                slot.Count -= toRemove;
                remaining -= toRemove;

                if (slot.Count <= 0)
                    slot = InventorySlotElement.Empty;

                slots[i] = slot;
            }

            return true;
        }

        public static void SwapSlots(
            ref DynamicBuffer<InventorySlotElement> srcSlots, int srcIdx,
            ref DynamicBuffer<InventorySlotElement> dstSlots, int dstIdx)
        {
            var temp = srcSlots[srcIdx];
            srcSlots[srcIdx] = dstSlots[dstIdx];
            dstSlots[dstIdx] = temp;
        }

        public static int GetCount(in DynamicBuffer<InventorySlotElement> slots, int itemId, int effectiveCap)
        {
            int total = 0;
            int limit = math.min(slots.Length, effectiveCap);
            for (int i = 0; i < limit; i++)
            {
                if (slots[i].ItemId == itemId)
                    total += slots[i].Count;
            }
            return total;
        }

        public static int GetUsedSlotCount(in DynamicBuffer<InventorySlotElement> slots, int effectiveCap)
        {
            int used = 0;
            int limit = math.min(slots.Length, effectiveCap);
            for (int i = 0; i < limit; i++)
            {
                if (!slots[i].IsEmpty)
                    used++;
            }
            return used;
        }

        public static bool IsFull(in DynamicBuffer<InventorySlotElement> slots, int effectiveCap)
        {
            int limit = math.min(slots.Length, effectiveCap);
            for (int i = 0; i < limit; i++)
            {
                if (slots[i].IsEmpty)
                    return false;
            }
            return limit > 0;
        }
    }
}
