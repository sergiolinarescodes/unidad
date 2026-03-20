using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TimerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimerData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Single job: clear previous flags then tick
            new ClearAndTickJob { DeltaTime = dt }.ScheduleParallel();
            state.Dependency.Complete();

            // Destroy cancelled timers on main thread (requires sync)
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<TimerCancelled>>()
                    .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        partial struct ClearAndTickJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref TimerData timer, EnabledRefRW<TimerCompleted> completed)
            {
                // Clear previous frame's flag
                completed.ValueRW = false;

                if (timer.Paused)
                    return;

                timer.Elapsed += DeltaTime;

                if (timer.Elapsed >= timer.Duration)
                {
                    completed.ValueRW = true;

                    if (timer.Loop)
                    {
                        timer.Elapsed -= timer.Duration;
                    }
                    else
                    {
                        timer.Paused = true;
                    }
                }
            }
        }
    }
}
