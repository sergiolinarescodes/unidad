using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Singleton — global time of day. Ticked by WorldTimeSystem.
    /// TimeOfDay ranges 0..24 (hours). DayLength is real seconds per game day.
    /// </summary>
    public struct WorldTimeData : IComponentData
    {
        public float TimeOfDay;
        public float DayLength;
        public int CurrentDay;
        public float TimeScale;

        public static WorldTimeData Default => new WorldTimeData
        {
            TimeOfDay = 6f,
            DayLength = 600f, // 10 minutes = 1 game day
            CurrentDay = 1,
            TimeScale = 1f
        };
    }

    /// <summary>
    /// Per-agent schedule reference. Points to a schedule definition entity.
    /// </summary>
    public struct ScheduleData : IComponentData
    {
        public int ScheduleId;
        public int CurrentSlotIndex;
    }

    /// <summary>
    /// Schedule definition entity identity.
    /// </summary>
    public struct ScheduleDefinition : IComponentData
    {
        public int ScheduleId;
        public FixedString64Bytes DebugName;
    }

    /// <summary>
    /// One time slot in a daily schedule. Stored as a buffer on schedule definition entities.
    /// Overnight slots: StartTime > EndTime (e.g., 22..6 crosses midnight).
    /// </summary>
    public struct ScheduleSlotElement : IBufferElementData
    {
        public float StartTime;
        public float EndTime;
        public int RequiredStateId;
        public int StrategyOverrideId;
        public int PriorityActionId;
    }

    /// <summary>1-frame event: agent's schedule slot changed this frame.</summary>
    public struct ScheduleSlotChanged : IComponentData, IEnableableComponent { }
}
