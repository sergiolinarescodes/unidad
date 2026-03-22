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
    [UpdateAfter(typeof(ActionQueueSystem))]
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
                    continue;
                }

                // --- Handle navigation completion ---
                if (actionState.ValueRO.Phase == AgentActionPhase.Navigating &&
                    em.HasComponent<PathCompleted>(entity) &&
                    em.IsComponentEnabled<PathCompleted>(entity))
                {
                    actionState.ValueRW.Phase = AgentActionPhase.Executing;

                    if (em.HasBuffer<CommandEntry>(entity))
                    {
                        var commandQueue = em.GetBuffer<CommandEntry>(entity);
                        commandQueue.Add(new CommandEntry
                        {
                            Type = (CommandType)actionState.ValueRO.CurrentActionType,
                            Status = CommandStatus.Pending,
                            Duration = 0f,
                            Elapsed = 0f,
                            IntParam = actionState.ValueRO.CurrentActionId
                        });
                    }
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
                    }

                    int newActionId = scoringResult.ValueRO.BestActionId;

                    Entity strategyEntity = StrategyUtility.FindStrategyEntity(
                        in strategyEntities, in strategyDatas, agent.ValueRO.StrategyId);

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
