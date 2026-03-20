using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ResourceEventClearSystem : ISystem
    {
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
        public void OnUpdate(ref SystemState state)
        {
            new ProcessEventsJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct ProcessEventsJob : IJobEntity
        {
            void Execute(
                in DynamicBuffer<ResourceChangeRecord> changes,
                in DynamicBuffer<ResourceElement> resources,
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

                    // Find current resource to get effective min
                    for (int j = 0; j < resources.Length; j++)
                    {
                        if (resources[j].ResourceId != record.ResourceId)
                            continue;

                        if (record.NewValue <= resources[j].BaseMin)
                            depleted.ValueRW = true;

                        if (record.NewValue >= record.EffectiveMax)
                            filled.ValueRW = true;

                        break;
                    }
                }
            }
        }
    }
}
