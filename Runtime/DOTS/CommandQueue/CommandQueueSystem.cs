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
            new ClearAndProcessJob { DeltaTime = dt }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ClearAndProcessJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(
                ref CommandQueueData queue,
                ref DynamicBuffer<CommandEntry> commands,
                EnabledRefRW<CommandCompleted> completed,
                EnabledRefRW<CommandFailed> failed,
                EnabledRefRW<QueueEmpty> empty)
            {
                // Clear previous frame's flags
                completed.ValueRW = false;
                failed.ValueRW = false;
                empty.ValueRW = false;

                if (queue.IsPaused)
                    return;

                if (queue.CurrentIndex >= commands.Length)
                {
                    empty.ValueRW = true;
                    return;
                }

                var cmd = commands[queue.CurrentIndex];

                if (cmd.Status == CommandStatus.Pending)
                    cmd.Status = CommandStatus.Running;

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
                    }
                }

                commands[queue.CurrentIndex] = cmd;

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
