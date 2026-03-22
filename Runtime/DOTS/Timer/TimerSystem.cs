using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TimerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimerData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (timer, entity) in
                SystemAPI.Query<RefRW<TimerData>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<TimerCompleted>(entity, false);

                if (timer.ValueRO.Paused)
                    continue;

                timer.ValueRW.Elapsed += dt;

                if (timer.ValueRO.Elapsed >= timer.ValueRO.Duration)
                {
                    ecb.SetComponentEnabled<TimerCompleted>(entity, true);

                    if (timer.ValueRO.Loop)
                    {
                        timer.ValueRW.Elapsed -= timer.ValueRO.Duration;
                    }
                    else
                    {
                        timer.ValueRW.Paused = true;
                    }
                }
            }

            // Destroy cancelled timers
            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<TimerCancelled>>()
                    .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
