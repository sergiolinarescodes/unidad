using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Ticks WorldTimeData.TimeOfDay. Wraps at 24 → increments CurrentDay.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WorldTimeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldTimeData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            var timeData = SystemAPI.GetSingletonRW<WorldTimeData>();
            float dayLength = timeData.ValueRO.DayLength;
            if (dayLength <= 0f) return;

            float hoursPerSecond = 24f / dayLength * timeData.ValueRO.TimeScale;
            float newTime = timeData.ValueRO.TimeOfDay + hoursPerSecond * dt;

            if (newTime >= 24f)
            {
                newTime -= 24f;
                timeData.ValueRW.CurrentDay++;
            }

            timeData.ValueRW.TimeOfDay = newTime;
        }
    }

    /// <summary>
    /// Checks each agent's schedule against the current time of day.
    /// When the active slot changes, triggers state transitions and strategy overrides.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorldTimeSystem))]
    public partial struct ScheduleSystem : ISystem
    {
        EntityQuery _scheduleDefQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScheduleData>();
            state.RequireForUpdate<WorldTimeData>();

            _scheduleDefQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ScheduleDefinition>()
                .Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var worldTime = SystemAPI.GetSingleton<WorldTimeData>();
            float timeOfDay = worldTime.TimeOfDay;

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var schedEntities = _scheduleDefQuery.ToEntityArray(Allocator.Temp);
            var schedDatas = _scheduleDefQuery.ToComponentDataArray<ScheduleDefinition>(Allocator.Temp);

            foreach (var (schedData, entity) in
                SystemAPI.Query<RefRW<ScheduleData>>()
                    .WithNone<AgentIsSuspended>()
                    .WithEntityAccess())
            {
                // Find schedule definition entity
                Entity schedDefEntity = Entity.Null;
                for (int s = 0; s < schedDatas.Length; s++)
                {
                    if (schedDatas[s].ScheduleId == schedData.ValueRO.ScheduleId)
                    {
                        schedDefEntity = schedEntities[s];
                        break;
                    }
                }

                if (schedDefEntity == Entity.Null)
                    continue;

                var slots = em.GetBuffer<ScheduleSlotElement>(schedDefEntity);
                int newSlotIndex = ScheduleUtility.FindCurrentSlot(in slots, timeOfDay);

                if (newSlotIndex == schedData.ValueRO.CurrentSlotIndex)
                    continue; // No change

                int prevSlot = schedData.ValueRO.CurrentSlotIndex;
                schedData.ValueRW.CurrentSlotIndex = newSlotIndex;
                ecb.SetComponentEnabled<ScheduleSlotChanged>(entity, true);

                if (newSlotIndex < 0)
                    continue; // No active slot

                var slot = slots[newSlotIndex];

                // Trigger state transition
                if (slot.RequiredStateId >= 0 && em.HasComponent<StateMachineData>(entity))
                {
                    var sm = em.GetComponentData<StateMachineData>(entity);
                    sm.TransitionRequested = true;
                    sm.RequestedState = slot.RequiredStateId;
                    em.SetComponentData(entity, sm);
                }

                // Trigger strategy override
                if (slot.StrategyOverrideId >= 0 && em.HasComponent<StrategyAssignRequest>(entity))
                {
                    em.SetComponentData(entity, new StrategyAssignRequest
                    {
                        StrategyId = slot.StrategyOverrideId
                    });
                    ecb.SetComponentEnabled<StrategyAssignRequest>(entity, true);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            schedEntities.Dispose();
            schedDatas.Dispose();
        }
    }

    /// <summary>
    /// Clears ScheduleSlotChanged events from previous frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ScheduleEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScheduleData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<ScheduleData>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<ScheduleSlotChanged>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
