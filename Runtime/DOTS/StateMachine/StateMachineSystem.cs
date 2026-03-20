using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct StateMachineSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StateMachineData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame flags then process transitions
            new ClearFlagsJob().ScheduleParallel();
            state.Dependency.Complete();

            new ProcessTransitionsJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct ClearFlagsJob : IJobEntity
        {
            void Execute(
                EnabledRefRW<StateEntered> entered,
                EnabledRefRW<StateExited> exited)
            {
                entered.ValueRW = false;
                exited.ValueRW = false;
            }
        }

        [BurstCompile]
        partial struct ProcessTransitionsJob : IJobEntity
        {
            void Execute(
                ref StateMachineData sm,
                EnabledRefRW<StateEntered> entered,
                EnabledRefRW<StateExited> exited)
            {
                if (!sm.TransitionRequested)
                    return;

                sm.TransitionRequested = false;
                sm.PreviousState = sm.CurrentState;
                sm.CurrentState = sm.RequestedState;

                exited.ValueRW = true;
                entered.ValueRW = true;
            }
        }
    }
}
