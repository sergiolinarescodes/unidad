using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Bridges ScoringResult decisions into AgentActionState and CommandQueue.
    ///
    /// Execution contract (phase-based):
    ///   Starting    → resolve target, if navigation needed: set Navigating + enable PathRequest
    ///   Navigating  → PathFollowSystem handles movement; on PathCompleted: advance to Executing
    ///   Executing   → enqueue CommandEntry; on CommandCompleted: advance to Completing
    ///   Completing  → apply ActionEffects, update timestamps, fire ActionCompleted, reset
    ///
    /// Game code's responsibility: For custom CommandTypes (32+), create a system that runs
    /// after CommandQueueSystem and handles entries with those types.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ScoringSystem))]
    [UpdateAfter(typeof(CommandQueueSystem))]
    public partial struct AgentActionSystem : ISystem
    {
        EntityQuery _strategyDefQuery;
        EntityQuery _agentQuery;

        public void OnCreate(ref SystemState state)
        {
            _strategyDefQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<StrategyDefinition>()
                .Build(ref state);

            _agentQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<AgentActionState>()
                .WithAll<ScoringResult, AgentData, AgentPreconditions>()
                .Build(ref state);

            state.RequireForUpdate(_agentQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            double elapsedTime = SystemAPI.Time.ElapsedTime;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var strategyEntities = _strategyDefQuery.ToEntityArray(Allocator.Temp);
            var strategyDatas = _strategyDefQuery.ToComponentDataArray<StrategyDefinition>(Allocator.Temp);
            var strategyLookup = StrategyUtility.BuildStrategyLookup(
                in strategyEntities, in strategyDatas, Allocator.Temp);

            foreach (var (scoringResult, actionState, agent, preconditions,
                effects, entity) in
                SystemAPI.Query<
                    RefRO<ScoringResult>,
                    RefRW<AgentActionState>,
                    RefRO<AgentData>,
                    RefRO<AgentPreconditions>,
                    DynamicBuffer<ActionEffectElement>>()
                    .WithEntityAccess())
            {
                // --- Handle action completion from CommandQueue ---
                if (em.HasComponent<CommandCompleted>(entity) &&
                    em.IsComponentEnabled<CommandCompleted>(entity) &&
                    actionState.ValueRO.Phase == AgentActionPhase.Executing)
                {
                    if (em.HasComponent<StateMachineData>(entity) &&
                        em.HasBuffer<ResourceElement>(entity))
                    {
                        var sm = em.GetComponentData<StateMachineData>(entity);
                        var resources = em.GetBuffer<ResourceElement>(entity);
                        var resourceChanges = em.GetBuffer<ResourceChangeRecord>(entity);
                        var maxMods = em.GetBuffer<ResourceMaxModifier>(entity);
                        var minMods = em.GetBuffer<ResourceMinModifier>(entity);

                        AgentActionUtility.ApplyAllEffects(
                            in effects, ref resources, ref resourceChanges,
                            in maxMods, in minMods, ref sm);
                        em.SetComponentData(entity, sm);
                    }

                    var timestamps = em.GetBuffer<ActionTimestampElement>(entity);
                    int tsIdx = ScoringUtility.FindTimestamp(in timestamps, actionState.ValueRO.CurrentActionId);
                    if (tsIdx >= 0)
                    {
                        var ts = timestamps[tsIdx];
                        ts.LastCompletedTime = elapsedTime;
                        timestamps[tsIdx] = ts;
                    }

                    var completionRecords = em.GetBuffer<ActionCompletionRecord>(entity);
                    completionRecords.Add(new ActionCompletionRecord
                    {
                        ActionId = actionState.ValueRO.CurrentActionId,
                        ActionType = actionState.ValueRO.CurrentActionType,
                        CompletedTime = elapsedTime,
                        WasSuccessful = true
                    });

                    actionState.ValueRW.Phase = AgentActionPhase.None;
                    actionState.ValueRW.CurrentActionId = -1;
                    ecb.SetComponentEnabled<ActionCompleted>(entity, true);

                    // Restore rescoring now that action is done
                    if (em.HasComponent<ActionQueueConfig>(entity))
                    {
                        var cfg = em.GetComponentData<ActionQueueConfig>(entity);
                        cfg.AllowRescore = true;
                        em.SetComponentData(entity, cfg);
                    }
                    continue;
                }

                // --- Handle navigation completion ---
                // Check both PathCompleted (1-frame event) and NavAgentStatus.Arrived (persistent).
                // PathCompleted may be cleared by NavEventClearSystem before this system runs.
                bool navArrived = false;
                if (actionState.ValueRO.Phase == AgentActionPhase.Navigating)
                {
                    if (em.HasComponent<PathCompleted>(entity) &&
                        em.IsComponentEnabled<PathCompleted>(entity))
                        navArrived = true;
                    else if (em.HasComponent<NavAgent>(entity) &&
                        em.GetComponentData<NavAgent>(entity).Status == NavAgentStatus.Arrived)
                        navArrived = true;
                }
                if (navArrived)
                {
                    actionState.ValueRW.Phase = AgentActionPhase.Executing;

                    // Use mapped duration when ActionBridgeConfig exists, otherwise 3s fallback
                    float waitDuration = 3f;
                    if (SystemAPI.HasSingleton<ActionBridgeConfig>())
                    {
                        var mappingEntity = SystemAPI.GetSingletonEntity<ActionBridgeConfig>();
                        if (em.HasBuffer<ActionTargetMappingElement>(mappingEntity))
                        {
                            var mappings = em.GetBuffer<ActionTargetMappingElement>(mappingEntity);
                            int mapIdx = ActionBridgeUtility.FindMapping(
                                in mappings, actionState.ValueRO.CurrentActionType);
                            if (mapIdx >= 0)
                                waitDuration = mappings[mapIdx].ExecutionDuration;
                            else
                                waitDuration = SystemAPI.GetSingleton<ActionBridgeConfig>().DefaultInPlaceDuration;
                        }
                    }

                    ActionBridgeUtility.EnqueueWaitCommand(em, entity, waitDuration);

                    // Claim POI at arrival
                    if (em.HasComponent<AgentTarget>(entity))
                        ActionBridgeUtility.ClaimPOI(em, entity,
                            em.GetComponentData<AgentTarget>(entity).TargetEntity);

                    continue;
                }

                // --- Handle new action selection (SingleAction mode) ---
                if (scoringResult.ValueRO.ActionChanged &&
                    scoringResult.ValueRO.BestActionId >= 0 &&
                    actionState.ValueRO.CurrentActionId != scoringResult.ValueRO.BestActionId)
                {
                    if (actionState.ValueRO.Phase != AgentActionPhase.None)
                    {
                        var interruptRecords = em.GetBuffer<ActionCompletionRecord>(entity);
                        interruptRecords.Add(new ActionCompletionRecord
                        {
                            ActionId = actionState.ValueRO.CurrentActionId,
                            ActionType = actionState.ValueRO.CurrentActionType,
                            CompletedTime = elapsedTime,
                            WasSuccessful = false
                        });
                        ecb.SetComponentEnabled<ActionInterrupted>(entity, true);

                        // Restore rescoring for the new action selection
                        if (em.HasComponent<ActionQueueConfig>(entity))
                        {
                            var cfg = em.GetComponentData<ActionQueueConfig>(entity);
                            cfg.AllowRescore = true;
                            em.SetComponentData(entity, cfg);
                        }
                    }

                    int newActionId = scoringResult.ValueRO.BestActionId;

                    Entity strategyEntity = StrategyUtility.FindStrategyEntity(
                        in strategyLookup, agent.ValueRO.StrategyId);

                    if (strategyEntity != Entity.Null)
                    {
                        var strategyActions = SystemAPI.GetBuffer<StrategyActionElement>(strategyEntity);
                        int actionType = -1;
                        for (int a = 0; a < strategyActions.Length; a++)
                        {
                            if (strategyActions[a].ActionId == newActionId)
                            {
                                if (!StrategyUtility.CheckPreconditions(
                                    strategyActions[a].PreconditionFlags,
                                    preconditions.ValueRO.AvailableFlags))
                                    continue;

                                actionType = strategyActions[a].ActionType;
                                break;
                            }
                        }

                        if (actionType >= 0)
                        {
                            actionState.ValueRW.CurrentActionId = newActionId;
                            actionState.ValueRW.CurrentActionType = actionType;
                            actionState.ValueRW.Phase = AgentActionPhase.Starting;
                            actionState.ValueRW.ActionStartTime = (float)elapsedTime;

                            effects.Clear();
                            if (SystemAPI.HasBuffer<StrategyActionEffectTemplate>(strategyEntity))
                            {
                                var effectTemplates = SystemAPI.GetBuffer<StrategyActionEffectTemplate>(strategyEntity);
                                for (int e = 0; e < effectTemplates.Length; e++)
                                {
                                    if (effectTemplates[e].ActionId == newActionId)
                                    {
                                        effects.Add(new ActionEffectElement
                                        {
                                            EffectType = effectTemplates[e].EffectType,
                                            TargetResourceId = effectTemplates[e].TargetResourceId,
                                            Value = effectTemplates[e].Value
                                        });
                                    }
                                }
                            }

                            ecb.SetComponentEnabled<ActionStarted>(entity, true);
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            strategyEntities.Dispose();
            strategyDatas.Dispose();
        }
    }
}
