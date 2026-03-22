using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Clears action and queue events from the previous frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ActionEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AgentActionState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<AgentActionState>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<ActionStarted>(entity, false);
                ecb.SetComponentEnabled<ActionCompleted>(entity, false);
                ecb.SetComponentEnabled<ActionInterrupted>(entity, false);
            }

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<ActionQueueConfig>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<QueueAdvanced>(entity, false);
                ecb.SetComponentEnabled<QueueCompleted>(entity, false);
                ecb.SetComponentEnabled<QueueInterrupted>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Manages action queue advancement and interrupt policy enforcement.
    /// Pass A: Only SingleAction mode is active.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ScoringSystem))]
    public partial struct ActionQueueSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActionQueueConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Pass A: SingleAction mode — no queue logic needed.
            // FUTURE (Pass B): Process queue advancement here.
        }
    }
}
