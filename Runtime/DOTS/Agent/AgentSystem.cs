using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Clears agent lifecycle and activity event tags from the previous frame.
    /// Runs OrderFirst so all downstream systems see clean state.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct AgentEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AgentData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<AgentData>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<AgentSpawned>(entity, false);
                ecb.SetComponentEnabled<AgentActivated>(entity, false);
                ecb.SetComponentEnabled<AgentSuspended>(entity, false);
                ecb.SetComponentEnabled<AgentDespawning>(entity, false);
            }

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<AgentActivity>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<ActivityChanged>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
