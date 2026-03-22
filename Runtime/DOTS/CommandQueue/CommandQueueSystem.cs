using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CommandQueueSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CommandQueueData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            float dt = SystemAPI.Time.DeltaTime;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (queue, entity) in
                SystemAPI.Query<RefRW<CommandQueueData>>()
                    .WithAll<CommandEntry>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<CommandCompleted>(entity, false);
                ecb.SetComponentEnabled<CommandFailed>(entity, false);
                ecb.SetComponentEnabled<QueueEmpty>(entity, false);

                if (queue.ValueRO.IsPaused)
                    continue;

                var commands = em.GetBuffer<CommandEntry>(entity);

                if (queue.ValueRO.CurrentIndex >= commands.Length)
                {
                    ecb.SetComponentEnabled<QueueEmpty>(entity, true);
                    continue;
                }

                var cmd = commands[queue.ValueRO.CurrentIndex];

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
                            cmd.Elapsed += dt;
                            if (cmd.Elapsed >= cmd.Duration)
                                cmd.Status = CommandStatus.Completed;
                            break;
                    }
                }

                commands[queue.ValueRO.CurrentIndex] = cmd;

                if (cmd.Status == CommandStatus.Completed)
                {
                    ecb.SetComponentEnabled<CommandCompleted>(entity, true);
                    queue.ValueRW.CurrentIndex++;

                    if (queue.ValueRO.CurrentIndex >= commands.Length)
                        ecb.SetComponentEnabled<QueueEmpty>(entity, true);
                }
                else if (cmd.Status == CommandStatus.Failed)
                {
                    ecb.SetComponentEnabled<CommandFailed>(entity, true);
                    queue.ValueRW.CurrentIndex++;

                    if (queue.ValueRO.CurrentIndex >= commands.Length)
                        ecb.SetComponentEnabled<QueueEmpty>(entity, true);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
