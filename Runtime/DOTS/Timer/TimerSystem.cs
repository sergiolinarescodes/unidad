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

            // Clear previous frame's completion flags
            new ClearCompletedJob().ScheduleParallel();
            state.Dependency.Complete();

            // Tick active timers
            new TickTimersJob { DeltaTime = dt }.ScheduleParallel();
            state.Dependency.Complete();

            // Destroy cancelled timers (query only matches entities where TimerCancelled is enabled)
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
        partial struct ClearCompletedJob : IJobEntity
        {
            void Execute(EnabledRefRW<TimerCompleted> completed)
            {
                completed.ValueRW = false;
            }
        }

        [BurstCompile]
        partial struct TickTimersJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref TimerData timer, EnabledRefRW<TimerCompleted> completed)
            {
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
