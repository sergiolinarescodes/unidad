using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Clears action and queue events from the previous frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ActionEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AgentActionState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<AgentActionState>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<ActionStarted>(entity, false);
                ecb.SetComponentEnabled<ActionCompleted>(entity, false);
                ecb.SetComponentEnabled<ActionInterrupted>(entity, false);
            }

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<ActionQueueConfig>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<QueueAdvanced>(entity, false);
                ecb.SetComponentEnabled<QueueCompleted>(entity, false);
                ecb.SetComponentEnabled<QueueInterrupted>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Manages action queue advancement and interrupt policy enforcement.
    ///
    /// SingleAction mode: passes through to AgentActionSystem (no queue logic).
    /// QueueFromStrategy: when scoring selects an action with plan entries, populates queue.
    ///     Queue advances automatically when each step's CommandCompleted fires.
    /// QueueManual: game code enqueues via ActionQueueUtility. System advances on completion.
    ///
    /// Interrupt policies control what happens when scoring picks a different action mid-queue:
    ///   Immediate     — clear queue, switch to new action
    ///   FinishCurrent — finish current step, then switch
    ///   FinishQueue   — complete all steps before accepting new action
    ///   PriorityBased — only interrupt if score delta exceeds threshold
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AgentActionSystem))]
    public partial struct ActionQueueSystem : ISystem
    {
        EntityQuery _strategyDefQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActionQueueConfig>();

            _strategyDefQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<StrategyDefinition>()
                .Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var strategyEntities = _strategyDefQuery.ToEntityArray(Allocator.Temp);
            var strategyDatas = _strategyDefQuery.ToComponentDataArray<StrategyDefinition>(Allocator.Temp);
            var strategyLookup = StrategyUtility.BuildStrategyLookup(
                in strategyEntities, in strategyDatas, Allocator.Temp);

            foreach (var (config, actionState, scoringResult, agent, entity) in
                SystemAPI.Query<
                    RefRO<ActionQueueConfig>,
                    RefRW<AgentActionState>,
                    RefRO<ScoringResult>,
                    RefRO<AgentData>>()
                    .WithEntityAccess())
            {
                var mode = config.ValueRO.Mode;

                // SingleAction: no queue logic — AgentActionSystem handles everything
                if (mode == ActionQueueMode.SingleAction)
                    continue;

                var queueProgress = em.GetComponentData<ActionQueueProgress>(entity);
                var queue = em.GetBuffer<ActionQueueEntry>(entity);

                // --- Handle queue advancement on action completion ---
                if (em.HasComponent<ActionCompleted>(entity) &&
                    em.IsComponentEnabled<ActionCompleted>(entity) &&
                    queue.Length > 0)
                {
                    // Mark current entry as completed
                    if (queueProgress.CurrentIndex < queue.Length)
                    {
                        var entry = queue[queueProgress.CurrentIndex];
                        entry.Status = ActionQueueEntryStatus.Completed;
                        queue[queueProgress.CurrentIndex] = entry;
                    }

                    queueProgress.CurrentIndex++;

                    if (queueProgress.CurrentIndex >= queue.Length)
                    {
                        // Queue finished
                        ecb.SetComponentEnabled<QueueCompleted>(entity, true);
                        queueProgress.TotalEntries = 0;
                        queue.Clear();
                        em.SetComponentData(entity, queueProgress);

                        // Allow rescoring again
                        if (!config.ValueRO.AllowRescore)
                        {
                            var cfg = config.ValueRO;
                            cfg.AllowRescore = true;
                            em.SetComponentData(entity, cfg);
                        }
                        continue;
                    }

                    // Advance to next entry
                    ecb.SetComponentEnabled<QueueAdvanced>(entity, true);

                    var nextEntry = queue[queueProgress.CurrentIndex];
                    nextEntry.Status = ActionQueueEntryStatus.Active;
                    queue[queueProgress.CurrentIndex] = nextEntry;

                    // Update action state for the next step
                    actionState.ValueRW.CurrentActionId = nextEntry.ActionId;
                    actionState.ValueRW.CurrentActionType = nextEntry.ActionType;
                    actionState.ValueRW.Phase = AgentActionPhase.Starting;
                    em.SetComponentData(entity, queueProgress);
                    continue;
                }

                // --- Handle new action from scoring (interrupt check) ---
                if (scoringResult.ValueRO.ActionChanged &&
                    scoringResult.ValueRO.BestActionId >= 0 &&
                    queue.Length > 0 &&
                    !queueProgress.QueuePaused)
                {
                    bool shouldInterrupt = EvaluateInterrupt(
                        config.ValueRO.InterruptPolicy,
                        config.ValueRO.InterruptPriorityThreshold,
                        scoringResult.ValueRO.BestScore,
                        actionState.ValueRO);

                    if (shouldInterrupt)
                    {
                        // Clear queue, fire interrupt
                        queue.Clear();
                        queueProgress.CurrentIndex = 0;
                        queueProgress.TotalEntries = 0;
                        em.SetComponentData(entity, queueProgress);
                        ecb.SetComponentEnabled<QueueInterrupted>(entity, true);

                        // AgentActionSystem will handle the new action from ScoringResult
                    }
                    else
                    {
                        // Queue not interrupted — continue current plan
                        em.SetComponentData(entity, queueProgress);
                        continue;
                    }
                }

                // --- QueueFromStrategy: populate queue when a new action with plan is selected ---
                if (mode == ActionQueueMode.QueueFromStrategy &&
                    queue.Length == 0 &&
                    scoringResult.ValueRO.ActionChanged &&
                    scoringResult.ValueRO.BestActionId >= 0)
                {
                    int actionId = scoringResult.ValueRO.BestActionId;

                    Entity strategyEntity = StrategyUtility.FindStrategyEntity(
                        in strategyLookup, agent.ValueRO.StrategyId);

                    if (strategyEntity != Entity.Null &&
                        em.HasBuffer<StrategyActionPlanEntry>(strategyEntity))
                    {
                        var planEntries = em.GetBuffer<StrategyActionPlanEntry>(strategyEntity);

                        // Check if this action has plan entries
                        bool hasPlan = false;
                        for (int i = 0; i < planEntries.Length; i++)
                        {
                            if (planEntries[i].ActionId == actionId)
                            {
                                hasPlan = true;
                                break;
                            }
                        }

                        if (hasPlan)
                        {
                            var agentTarget = em.HasComponent<AgentTarget>(entity)
                                ? em.GetComponentData<AgentTarget>(entity)
                                : default;

                            ActionQueueUtility.PopulateFromPlan(
                                ref queue, ref queueProgress,
                                in planEntries, actionId,
                                agentTarget.TargetPosition);

                            // Mark first entry as active
                            if (queue.Length > 0)
                            {
                                var first = queue[0];
                                first.Status = ActionQueueEntryStatus.Active;
                                queue[0] = first;

                                actionState.ValueRW.CurrentActionId = first.ActionId;
                                actionState.ValueRW.CurrentActionType = first.ActionType;
                                actionState.ValueRW.Phase = AgentActionPhase.Starting;

                                ecb.SetComponentEnabled<ActionStarted>(entity, true);
                            }

                            em.SetComponentData(entity, queueProgress);

                            // Disable rescoring while queue is active (unless AllowRescore is explicitly true)
                            if (config.ValueRO.InterruptPolicy == InterruptPolicy.FinishQueue)
                            {
                                var cfg = config.ValueRO;
                                cfg.AllowRescore = false;
                                em.SetComponentData(entity, cfg);
                            }
                        }
                    }
                }

                em.SetComponentData(entity, queueProgress);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            strategyEntities.Dispose();
            strategyDatas.Dispose();
        }

        static bool EvaluateInterrupt(InterruptPolicy policy, float threshold,
            float newScore, in AgentActionState currentAction)
        {
            if (currentAction.Phase == AgentActionPhase.None)
                return true; // No action running, always accept

            switch (policy)
            {
                case InterruptPolicy.Immediate:
                    return true;

                case InterruptPolicy.FinishCurrent:
                    // Only interrupt after current step completes (handled by caller checking ActionCompleted)
                    return false;

                case InterruptPolicy.FinishQueue:
                    return false;

                case InterruptPolicy.PriorityBased:
                    return newScore > threshold;

                default:
                    return true;
            }
        }
    }
}
