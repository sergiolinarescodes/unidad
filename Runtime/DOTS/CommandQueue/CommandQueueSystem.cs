using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CommandQueueSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CommandQueueData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Clear previous frame flags
            new ClearFlagsJob().ScheduleParallel();
            state.Dependency.Complete();

            // Process command queues
            new ProcessQueueJob { DeltaTime = dt }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ClearFlagsJob : IJobEntity
        {
            void Execute(
                EnabledRefRW<CommandCompleted> completed,
                EnabledRefRW<CommandFailed> failed,
                EnabledRefRW<QueueEmpty> empty)
            {
                completed.ValueRW = false;
                failed.ValueRW = false;
                empty.ValueRW = false;
            }
        }

        [BurstCompile]
        partial struct ProcessQueueJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(
                ref CommandQueueData queue,
                ref DynamicBuffer<CommandEntry> commands,
                EnabledRefRW<CommandCompleted> completed,
                EnabledRefRW<CommandFailed> failed,
                EnabledRefRW<QueueEmpty> empty)
            {
                if (queue.IsPaused)
                    return;

                if (queue.CurrentIndex >= commands.Length)
                {
                    empty.ValueRW = true;
                    return;
                }

                var cmd = commands[queue.CurrentIndex];

                // Start pending commands
                if (cmd.Status == CommandStatus.Pending)
                    cmd.Status = CommandStatus.Running;

                // Process built-in command types
                if (cmd.Status == CommandStatus.Running)
                {
                    switch (cmd.Type)
                    {
                        case CommandType.None:
                            cmd.Status = CommandStatus.Completed;
                            break;

                        case CommandType.Wait:
                            cmd.Elapsed += DeltaTime;
                            if (cmd.Elapsed >= cmd.Duration)
                                cmd.Status = CommandStatus.Completed;
                            break;

                        // Game-specific types are handled by game systems
                        // that run before this system and set Status directly
                    }
                }

                commands[queue.CurrentIndex] = cmd;

                // Advance on completion or failure
                if (cmd.Status == CommandStatus.Completed)
                {
                    completed.ValueRW = true;
                    queue.CurrentIndex++;

                    if (queue.CurrentIndex >= commands.Length)
                        empty.ValueRW = true;
                }
                else if (cmd.Status == CommandStatus.Failed)
                {
                    failed.ValueRW = true;
                    queue.CurrentIndex++;

                    if (queue.CurrentIndex >= commands.Length)
                        empty.ValueRW = true;
                }
            }
        }
    }
}
