using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Syncs AgentActivity from AgentActionState, NavAgent, and ActionQueueProgress.
    /// Provides a single readable "what is this agent doing" for game code and UI.
    /// Runs after PathFollowSystem so it reads final navigation state for the frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PathFollowSystem))]
    public partial struct AgentActivitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AgentActivity>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (activity, actionState, entity) in
                SystemAPI.Query<
                    RefRW<AgentActivity>,
                    RefRO<AgentActionState>>()
                    .WithEntityAccess())
            {
                var prev = activity.ValueRO.CurrentActivity;
                var phase = actionState.ValueRO.Phase;

                AgentActivityType newActivity;
                int actionId = actionState.ValueRO.CurrentActionId;
                int actionType = actionState.ValueRO.CurrentActionType;
                float3 targetPos = float3.zero;
                Entity targetEntity = Entity.Null;

                // Read target info
                if (em.HasComponent<AgentTarget>(entity))
                {
                    var target = em.GetComponentData<AgentTarget>(entity);
                    targetPos = target.TargetPosition;
                    targetEntity = target.TargetEntity;
                }

                switch (phase)
                {
                    case AgentActionPhase.None:
                        // Check if queue has pending entries
                        if (em.HasComponent<ActionQueueProgress>(entity))
                        {
                            var progress = em.GetComponentData<ActionQueueProgress>(entity);
                            if (progress.TotalEntries > 0 && progress.CurrentIndex < progress.TotalEntries)
                            {
                                newActivity = AgentActivityType.Queued;
                                break;
                            }
                        }
                        newActivity = AgentActivityType.Idle;
                        break;

                    case AgentActionPhase.Starting:
                        newActivity = AgentActivityType.Queued;
                        break;

                    case AgentActionPhase.Navigating:
                        newActivity = AgentActivityType.Moving;
                        // Use current path destination as target
                        if (em.HasComponent<PathProgress>(entity) &&
                            em.HasBuffer<PathNodeElement>(entity))
                        {
                            var progress = em.GetComponentData<PathProgress>(entity);
                            var pathNodes = em.GetBuffer<PathNodeElement>(entity);
                            if (progress.CurrentPathIndex < pathNodes.Length)
                                targetPos = pathNodes[pathNodes.Length - 1].WorldPosition; // Final destination
                        }
                        break;

                    case AgentActionPhase.Executing:
                        newActivity = AgentActivityType.PerformingAction;
                        break;

                    case AgentActionPhase.Completing:
                        newActivity = AgentActivityType.PerformingAction;
                        break;

                    case AgentActionPhase.WaitingForCompletion:
                        newActivity = AgentActivityType.WaitingForInteraction;
                        break;

                    case AgentActionPhase.Interrupted:
                        newActivity = AgentActivityType.Idle;
                        actionId = -1;
                        actionType = 0;
                        break;

                    default:
                        newActivity = AgentActivityType.Idle;
                        break;
                }

                // Update duration/elapsed from CommandQueue if executing
                float duration = activity.ValueRO.ActionDuration;
                float elapsed = activity.ValueRO.ActionElapsed;
                float progress2 = 0f;

                if (phase == AgentActionPhase.Executing &&
                    em.HasComponent<CommandQueueData>(entity) &&
                    em.HasBuffer<CommandEntry>(entity))
                {
                    var queueData = em.GetComponentData<CommandQueueData>(entity);
                    var commands = em.GetBuffer<CommandEntry>(entity);
                    if (queueData.CurrentIndex < commands.Length)
                    {
                        var cmd = commands[queueData.CurrentIndex];
                        duration = cmd.Duration;
                        elapsed = cmd.Elapsed;
                        progress2 = duration > 0f ? math.clamp(elapsed / duration, 0f, 1f) : 1f;
                    }
                }
                else if (newActivity == AgentActivityType.Idle || newActivity == AgentActivityType.Queued)
                {
                    duration = 0f;
                    elapsed = 0f;
                    progress2 = 0f;
                }

                activity.ValueRW.CurrentActivity = newActivity;
                activity.ValueRW.CurrentActionId = actionId;
                activity.ValueRW.CurrentActionType = actionType;
                activity.ValueRW.ActionDuration = duration;
                activity.ValueRW.ActionElapsed = elapsed;
                activity.ValueRW.ActionProgress = progress2;
                activity.ValueRW.ActivityTargetPosition = targetPos;
                activity.ValueRW.ActivityTargetEntity = targetEntity;

                if (newActivity != prev)
                    ecb.SetComponentEnabled<ActivityChanged>(entity, true);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
