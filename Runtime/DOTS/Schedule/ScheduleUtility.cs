using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class ScheduleUtility
    {
        /// <summary>
        /// Finds the active slot index for a given time of day.
        /// Handles overnight wrap (StartTime > EndTime, e.g., 22..6).
        /// Returns -1 if no slot matches.
        /// </summary>
        public static int FindCurrentSlot(in DynamicBuffer<ScheduleSlotElement> slots, float timeOfDay)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.StartTime <= slot.EndTime)
                {
                    // Normal slot (e.g., 8..17)
                    if (timeOfDay >= slot.StartTime && timeOfDay < slot.EndTime)
                        return i;
                }
                else
                {
                    // Overnight slot (e.g., 22..6)
                    if (timeOfDay >= slot.StartTime || timeOfDay < slot.EndTime)
                        return i;
                }
            }
            return -1;
        }
    }
}
