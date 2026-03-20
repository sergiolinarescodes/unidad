using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ResourceEventClearSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourceElement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ClearEventsJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct ClearEventsJob : IJobEntity
        {
            void Execute(
                ref DynamicBuffer<ResourceChangeRecord> changes,
                EnabledRefRW<ResourceChanged> changed,
                EnabledRefRW<ResourceDepleted> depleted,
                EnabledRefRW<ResourceFilled> filled)
            {
                changes.Clear();
                changed.ValueRW = false;
                depleted.ValueRW = false;
                filled.ValueRW = false;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct ResourceEventSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourceElement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ProcessEventsJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct ProcessEventsJob : IJobEntity
        {
            void Execute(
                in DynamicBuffer<ResourceChangeRecord> changes,
                EnabledRefRW<ResourceChanged> changed,
                EnabledRefRW<ResourceDepleted> depleted,
                EnabledRefRW<ResourceFilled> filled)
            {
                if (changes.Length == 0)
                    return;

                changed.ValueRW = true;

                for (int i = 0; i < changes.Length; i++)
                {
                    var record = changes[i];

                    // Fire depleted only on downward threshold crossing
                    if (record.NewValue <= record.EffectiveMin && record.OldValue > record.EffectiveMin)
                        depleted.ValueRW = true;

                    // Fire filled only on upward threshold crossing
                    if (record.NewValue >= record.EffectiveMax && record.OldValue < record.EffectiveMax)
                        filled.ValueRW = true;
                }
            }
        }
    }
}
