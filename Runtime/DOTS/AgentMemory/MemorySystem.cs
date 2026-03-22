using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>Clears memory events from previous frame.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct MemoryEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MemoryConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<MemoryConfig>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<MemoryAdded>(entity, false);
                ecb.SetComponentEnabled<MemoryForgotten>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Decays memory importance over time. Removes memories below ImportanceThreshold.
    /// Runs OrderLast so all other systems have had a chance to add memories first.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct MemoryDecaySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MemoryConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (config, entity) in
                SystemAPI.Query<RefRO<MemoryConfig>>()
                    .WithEntityAccess())
            {
                var memories = em.GetBuffer<MemoryElement>(entity);
                float decayAmount = config.ValueRO.DecayRate * dt;
                float threshold = config.ValueRO.ImportanceThreshold;
                bool anyForgotten = false;

                for (int i = memories.Length - 1; i >= 0; i--)
                {
                    var mem = memories[i];
                    mem.Importance -= decayAmount;

                    if (mem.Importance < threshold)
                    {
                        memories.RemoveAtSwapBack(i);
                        anyForgotten = true;
                    }
                    else
                    {
                        memories[i] = mem;
                    }
                }

                if (anyForgotten)
                    ecb.SetComponentEnabled<MemoryForgotten>(entity, true);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
