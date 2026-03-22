using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Generic action-to-navigation bridge. Reads ActionTargetMappingElement to
    /// automatically handle Starting → Navigating → Executing phase transitions.
    ///
    /// Opt-in: only runs when an ActionBridgeConfig singleton exists.
    /// Game code that needs custom behavior for specific ActionTypes can mark them
    /// as HandledByFramework = false in the mapping and handle them in a separate system.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AgentActionSystem))]
    public partial struct ActionBridgeSystem : ISystem
    {
        EntityQuery _mappingQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActionBridgeConfig>();
            state.RequireForUpdate<AgentActionState>();

            _mappingQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ActionBridgeConfig>()
                .Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var config = SystemAPI.GetSingleton<ActionBridgeConfig>();

            var mappingEntity = _mappingQuery.GetSingletonEntity();
            var mappings = em.GetBuffer<ActionTargetMappingElement>(mappingEntity);

            foreach (var (actionState, agentTarget, locomotion, transform, agentData, entity) in
                SystemAPI.Query<
                    RefRW<AgentActionState>,
                    RefRW<AgentTarget>,
                    RefRO<AgentLocomotion>,
                    RefRO<LocalTransform>,
                    RefRO<AgentData>>()
                    .WithEntityAccess())
            {
                var phase = actionState.ValueRO.Phase;
                float3 agentPos = transform.ValueRO.Position;

                // --- Interrupted: restore AllowRescore and reset ---
                if (phase == AgentActionPhase.Interrupted)
                {
                    ActionBridgeUtility.ReleasePOI(em, entity);

                    if (em.HasComponent<ActionQueueConfig>(entity))
                    {
                        var cfg = em.GetComponentData<ActionQueueConfig>(entity);
                        if (!cfg.AllowRescore)
                        {
                            cfg.AllowRescore = true;
                            em.SetComponentData(entity, cfg);
                        }
                    }

                    actionState.ValueRW.Phase = AgentActionPhase.None;
                    actionState.ValueRW.CurrentActionId = -1;
                    continue;
                }

                // --- None: release any lingering POI claim ---
                if (phase == AgentActionPhase.None)
                {
                    ActionBridgeUtility.ReleasePOI(em, entity);
                    continue;
                }

                // --- Starting: resolve target and begin navigation or execute in place ---
                if (phase != AgentActionPhase.Starting)
                    continue;

                int actionType = actionState.ValueRO.CurrentActionType;
                int mappingIdx = ActionBridgeUtility.FindMapping(in mappings, actionType);

                if (mappingIdx >= 0 && !mappings[mappingIdx].HandledByFramework)
                    continue;

                if (config.LockScoringDuringExecution && em.HasComponent<ActionQueueConfig>(entity))
                {
                    var cfg = em.GetComponentData<ActionQueueConfig>(entity);
                    cfg.AllowRescore = false;
                    em.SetComponentData(entity, cfg);
                }

                int targetPOIType = mappingIdx >= 0 ? mappings[mappingIdx].TargetPOIType : -1;
                float execDuration = mappingIdx >= 0
                    ? mappings[mappingIdx].ExecutionDuration
                    : config.DefaultInPlaceDuration;

                if (targetPOIType < 0)
                {
                    actionState.ValueRW.Phase = AgentActionPhase.Executing;
                    ActionBridgeUtility.EnqueueWaitCommand(em, entity, execDuration);
                    continue;
                }

                if (!em.HasBuffer<KnownPOIElement>(entity))
                {
                    actionState.ValueRW.Phase = AgentActionPhase.Executing;
                    ActionBridgeUtility.EnqueueWaitCommand(em, entity, execDuration);
                    continue;
                }

                var knownPOIs = em.GetBuffer<KnownPOIElement>(entity);

                if (!ActionBridgeUtility.FindNearestPOI(in knownPOIs, targetPOIType, agentPos,
                    out float3 bestPos, out Entity bestPOI, out float bestDist))
                {
                    actionState.ValueRW.Phase = AgentActionPhase.Executing;
                    ActionBridgeUtility.EnqueueWaitCommand(em, entity, execDuration);
                    continue;
                }

                agentTarget.ValueRW.TargetEntity = bestPOI;
                agentTarget.ValueRW.TargetPosition = bestPos;

                if (bestDist <= locomotion.ValueRO.StoppingDistance * 2f)
                {
                    actionState.ValueRW.Phase = AgentActionPhase.Executing;
                    ActionBridgeUtility.EnqueueWaitCommand(em, entity, execDuration);
                    ActionBridgeUtility.ClaimPOI(em, entity, bestPOI);
                    continue;
                }

                actionState.ValueRW.Phase = AgentActionPhase.Navigating;
                if (em.HasComponent<PathRequest>(entity))
                {
                    em.SetComponentData(entity, new PathRequest
                    {
                        TargetNodeId = -1,
                        TargetWorldPosition = bestPos
                    });
                    ecb.SetComponentEnabled<PathRequest>(entity, true);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
