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
            new ClearAndTransitionJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct ClearAndTransitionJob : IJobEntity
        {
            void Execute(
                ref StateMachineData sm,
                EnabledRefRW<StateEntered> entered,
                EnabledRefRW<StateExited> exited)
            {
                // Clear previous frame's flags
                entered.ValueRW = false;
                exited.ValueRW = false;

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
