using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Processes StrategyAssignRequest. When a new strategy is assigned:
    /// 1. Clears the agent's ConsiderationElement buffer and copies from templates
    /// 2. Copies default StrategyParamElement values
    /// 3. Initializes ActionTimestampElement entries
    /// 4. Clears ActionQueueEntry and resets queue progress (if present)
    /// 5. Interrupts current action (if any) and resets ScoringResult
    ///
    /// Runs before ScoringSystem so new strategies take effect immediately.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct StrategyAssignmentSystem : ISystem
    {
        EntityQuery _strategyDefQuery;

        public void OnCreate(ref SystemState state)
        {
            _strategyDefQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<StrategyDefinition>()
                .Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var strategyEntities = _strategyDefQuery.ToEntityArray(Allocator.Temp);
            var strategyDatas = _strategyDefQuery.ToComponentDataArray<StrategyDefinition>(Allocator.Temp);
            var strategyLookup = StrategyUtility.BuildStrategyLookup(
                in strategyEntities, in strategyDatas, Allocator.Temp);
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, agent, considerations, timestamps, strategyParams, entity) in
                SystemAPI.Query<
                    RefRO<StrategyAssignRequest>,
                    RefRW<AgentData>,
                    DynamicBuffer<ConsiderationElement>,
                    DynamicBuffer<ActionTimestampElement>,
                    DynamicBuffer<StrategyParamElement>>()
                    .WithAll<StrategyAssignRequest>()
                    .WithEntityAccess())
            {
                int strategyId = request.ValueRO.StrategyId;
                Entity strategyEntity = StrategyUtility.FindStrategyEntity(
                    in strategyLookup, strategyId);

                if (strategyEntity == Entity.Null)
                {
                    ecb.SetComponentEnabled<StrategyAssignRequest>(entity, false);
                    continue;
                }

                // 1. Clear and repopulate considerations from templates
                considerations.Clear();
                var templates = SystemAPI.GetBuffer<StrategyConsiderationTemplate>(strategyEntity);
                for (int t = 0; t < templates.Length; t++)
                {
                    var tmpl = templates[t];
                    considerations.Add(new ConsiderationElement
                    {
                        ActionId = tmpl.ActionId,
                        InputType = tmpl.InputType,
                        InputParam = tmpl.InputParam,
                        CurveType = tmpl.CurveType,
                        CurveA = tmpl.CurveA,
                        CurveB = tmpl.CurveB,
                        CurveC = tmpl.CurveC,
                        CurveD = tmpl.CurveD
                    });
                }

                // 2. Initialize timestamps for actions
                timestamps.Clear();
                var actions = SystemAPI.GetBuffer<StrategyActionElement>(strategyEntity);
                for (int a = 0; a < actions.Length; a++)
                {
                    timestamps.Add(new ActionTimestampElement
                    {
                        ActionId = actions[a].ActionId,
                        LastCompletedTime = 0.0
                    });
                }

                // 3. Copy default strategy params
                if (SystemAPI.HasBuffer<StrategyParamElement>(strategyEntity))
                {
                    var defaultParams = SystemAPI.GetBuffer<StrategyParamElement>(strategyEntity);
                    strategyParams.Clear();
                    for (int p = 0; p < defaultParams.Length; p++)
                        strategyParams.Add(defaultParams[p]);
                }

                // 4. Clear action queue if present
                if (em.HasBuffer<ActionQueueEntry>(entity))
                    em.GetBuffer<ActionQueueEntry>(entity).Clear();
                if (em.HasComponent<ActionQueueProgress>(entity))
                    em.SetComponentData(entity, new ActionQueueProgress());

                // 5. Interrupt current action if running
                if (em.HasComponent<AgentActionState>(entity))
                {
                    var actionState = em.GetComponentData<AgentActionState>(entity);
                    if (actionState.Phase != AgentActionPhase.None)
                    {
                        actionState.Phase = AgentActionPhase.Interrupted;
                        actionState.CurrentActionId = -1;
                        em.SetComponentData(entity, actionState);
                        if (em.HasComponent<ActionInterrupted>(entity))
                            ecb.SetComponentEnabled<ActionInterrupted>(entity, true);
                    }
                }

                // 6. Reset scoring result
                if (em.HasComponent<ScoringResult>(entity))
                {
                    em.SetComponentData(entity, new ScoringResult
                    {
                        BestActionId = -1,
                        BestScore = -1f,
                        PreviousBestActionId = -1
                    });
                }

                agent.ValueRW.StrategyId = strategyId;
                ecb.SetComponentEnabled<StrategyAssignRequest>(entity, false);
                ecb.SetComponentEnabled<StrategyAssigned>(entity, true);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            strategyEntities.Dispose();
            strategyDatas.Dispose();
        }
    }
}
