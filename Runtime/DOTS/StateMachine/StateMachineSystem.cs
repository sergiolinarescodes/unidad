using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct StateMachineSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StateMachineData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            // Use ECB to defer enableable flag changes (avoids invalidating query iterator)
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (sm, entity) in
                SystemAPI.Query<RefRW<StateMachineData>>()
                    .WithEntityAccess())
            {
                // Clear previous frame's flags
                ecb.SetComponentEnabled<StateEntered>(entity, false);
                ecb.SetComponentEnabled<StateExited>(entity, false);

                if (!sm.ValueRO.TransitionRequested)
                    continue;

                sm.ValueRW.TransitionRequested = false;
                sm.ValueRW.PreviousState = sm.ValueRO.CurrentState;
                sm.ValueRW.CurrentState = sm.ValueRO.RequestedState;

                ecb.SetComponentEnabled<StateExited>(entity, true);
                ecb.SetComponentEnabled<StateEntered>(entity, true);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
