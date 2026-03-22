using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Evaluates all considerations for each agent and selects the highest-scoring action.
    /// Runs after StrategyAssignmentSystem so new strategies take effect, before ActionSystem.
    /// Uses SystemAPI.Query foreach (not IJobEntity) because agents have many buffer components.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StrategyAssignmentSystem))]
    public partial struct ScoringSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScoringResult>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            double elapsedTime = SystemAPI.Time.ElapsedTime;
            uint frameSeed = (uint)(elapsedTime * 1000.0) + 1u;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int entityIndex = 0;

            foreach (var (result, considerations, agent, target, transform, entity) in
                SystemAPI.Query<
                    RefRW<ScoringResult>,
                    DynamicBuffer<ConsiderationElement>,
                    RefRO<AgentData>,
                    RefRO<AgentTarget>,
                    RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                if (considerations.Length == 0)
                {
                    entityIndex++;
                    continue;
                }

                // Read secondary buffers via EntityManager
                var needs = em.GetBuffer<NeedElement>(entity);
                var resources = em.GetBuffer<ResourceElement>(entity);
                var maxMods = em.GetBuffer<ResourceMaxModifier>(entity);
                var minMods = em.GetBuffer<ResourceMinModifier>(entity);
                var timestamps = em.GetBuffer<ActionTimestampElement>(entity);
                var strategyParams = em.GetBuffer<StrategyParamElement>(entity);
                var contextSnapshot = em.GetBuffer<AgentContextSnapshot>(entity);

                int bestActionId = -1;
                float bestScore = -1f;

                // Process considerations in contiguous runs by ActionId
                int i = 0;
                while (i < considerations.Length)
                {
                    int currentActionId = considerations[i].ActionId;
                    float product = 1f;
                    int count = 0;
                    bool aborted = false;

                    while (i < considerations.Length && considerations[i].ActionId == currentActionId)
                    {
                        var c = considerations[i];
                        float input = ResolveInput(
                            c.InputType, c.InputParam,
                            in needs, in resources, in maxMods, in minMods,
                            in timestamps, in strategyParams, in contextSnapshot,
                            target.ValueRO, agent.ValueRO, transform.ValueRO,
                            elapsedTime, frameSeed, entityIndex);

                        float score = ScoringUtility.EvaluateCurve(
                            c.CurveType, input, c.CurveA, c.CurveB, c.CurveC, c.CurveD);

                        if (score <= 0f)
                        {
                            aborted = true;
                            while (i < considerations.Length && considerations[i].ActionId == currentActionId)
                                i++;
                            break;
                        }

                        product *= score;
                        count++;
                        i++;
                    }

                    if (aborted)
                        continue;

                    float finalScore = ScoringUtility.CompensatedScore(product, count);

                    if (finalScore > bestScore)
                    {
                        bestScore = finalScore;
                        bestActionId = currentActionId;
                    }
                }

                result.ValueRW.PreviousBestActionId = result.ValueRO.BestActionId;
                result.ValueRW.BestActionId = bestActionId;
                result.ValueRW.BestScore = bestScore;
                result.ValueRW.ActionChanged = bestActionId != result.ValueRO.PreviousBestActionId;

                if (result.ValueRO.ActionChanged)
                    ecb.SetComponentEnabled<ActionSelectionChanged>(entity, true);
                else
                    ecb.SetComponentEnabled<ActionSelectionChanged>(entity, false);

                entityIndex++;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        static float ResolveInput(
            ScoringInputType inputType, int inputParam,
            in DynamicBuffer<NeedElement> needs,
            in DynamicBuffer<ResourceElement> resources,
            in DynamicBuffer<ResourceMaxModifier> maxMods,
            in DynamicBuffer<ResourceMinModifier> minMods,
            in DynamicBuffer<ActionTimestampElement> timestamps,
            in DynamicBuffer<StrategyParamElement> strategyParams,
            in DynamicBuffer<AgentContextSnapshot> contextSnapshot,
            in AgentTarget target,
            in AgentData agent,
            in LocalTransform transform,
            double elapsedTime,
            uint frameSeed, int entityIndex)
        {
            switch (inputType)
            {
                case ScoringInputType.Constant:
                    return inputParam / 100f;

                case ScoringInputType.NeedLevel:
                {
                    float current = ResourceUtility.Get(in resources, inputParam);
                    float effMax = ResourceUtility.GetEffectiveMax(inputParam,
                        GetBaseMax(in resources, inputParam), in maxMods);
                    float effMin = ResourceUtility.GetEffectiveMin(inputParam,
                        GetBaseMin(in resources, inputParam), in minMods);
                    return NeedUtility.GetNormalizedDeficit(current, effMin, effMax);
                }

                case ScoringInputType.NeedUrgency:
                {
                    int idx = NeedUtility.FindNeed(in needs, inputParam);
                    if (idx < 0) return 0f;
                    return (float)needs[idx].CurrentUrgency / 3f;
                }

                case ScoringInputType.DistanceToTarget:
                {
                    float dist = math.distance(transform.Position, target.TargetPosition);
                    float maxRange = math.max(inputParam, 1f);
                    return math.clamp(dist / maxRange, 0f, 1f);
                }

                case ScoringInputType.TimeSinceAction:
                {
                    int idx = ScoringUtility.FindTimestamp(in timestamps, inputParam);
                    if (idx < 0) return 1f;
                    float elapsed = (float)(elapsedTime - timestamps[idx].LastCompletedTime);
                    return math.clamp(elapsed / 60f, 0f, 1f);
                }

                case ScoringInputType.ResourceLevel:
                {
                    float current = ResourceUtility.Get(in resources, inputParam);
                    float effMax = ResourceUtility.GetEffectiveMax(inputParam,
                        GetBaseMax(in resources, inputParam), in maxMods);
                    if (effMax <= 0f) return 0f;
                    return math.clamp(current / effMax, 0f, 1f);
                }

                case ScoringInputType.StrategyParam:
                    return StrategyUtility.GetParam(in strategyParams, inputParam);

                case ScoringInputType.AgentContext:
                case ScoringInputType.SharedContext:
                    return SharedContextUtility.GetFromSnapshot(in contextSnapshot, inputParam);

                case ScoringInputType.Random:
                {
                    uint seed = frameSeed * 1000u + (uint)entityIndex * 31u + (uint)inputParam;
                    var rng = new Random(math.max(seed, 1u));
                    return rng.NextFloat();
                }

                case ScoringInputType.NearbyPOICount:
                case ScoringInputType.AgentState:
                case ScoringInputType.WorldTime:
                    return StrategyUtility.GetParam(in strategyParams, inputParam);

                default:
                    return 0f;
            }
        }

        static float GetBaseMax(in DynamicBuffer<ResourceElement> resources, int resourceId)
        {
            for (int i = 0; i < resources.Length; i++)
                if (resources[i].ResourceId == resourceId)
                    return resources[i].BaseMax;
            return 100f;
        }

        static float GetBaseMin(in DynamicBuffer<ResourceElement> resources, int resourceId)
        {
            for (int i = 0; i < resources.Length; i++)
                if (resources[i].ResourceId == resourceId)
                    return resources[i].BaseMin;
            return 0f;
        }
    }
}
