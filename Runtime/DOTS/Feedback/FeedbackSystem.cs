using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>Clears feedback events.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct FeedbackEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AgentFeedback>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<AgentFeedback>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<FeedbackEvaluated>(entity, false);
                ecb.SetComponentEnabled<StrategyUnderperforming>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Periodically evaluates agent performance.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct FeedbackEvaluationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AgentFeedback>();
            state.RequireForUpdate<FeedbackConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            double elapsedTime = SystemAPI.Time.ElapsedTime;
            var config = SystemAPI.GetSingleton<FeedbackConfig>();
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (feedback, entity) in
                SystemAPI.Query<RefRW<AgentFeedback>>()
                    .WithEntityAccess())
            {
                var actionFeedbacks = em.GetBuffer<ActionFeedbackElement>(entity);
                var completionRecords = em.GetBuffer<ActionCompletionRecord>(entity);
                var needs = em.GetBuffer<NeedElement>(entity);

                for (int i = 0; i < completionRecords.Length; i++)
                {
                    var record = completionRecords[i];
                    int afIdx = FindActionFeedback(in actionFeedbacks, record.ActionId);

                    if (afIdx < 0)
                    {
                        actionFeedbacks.Add(new ActionFeedbackElement
                        {
                            ActionId = record.ActionId,
                            CompletionCount = record.WasSuccessful ? 1 : 0,
                            FailureCount = record.WasSuccessful ? 0 : 1,
                            CumulativeReward = record.WasSuccessful
                                ? config.RewardPerActionComplete : config.PenaltyPerActionFail,
                            AverageReward = record.WasSuccessful
                                ? config.RewardPerActionComplete : config.PenaltyPerActionFail
                        });
                    }
                    else
                    {
                        var af = actionFeedbacks[afIdx];
                        if (record.WasSuccessful)
                        {
                            af.CompletionCount++;
                            af.CumulativeReward += config.RewardPerActionComplete;
                        }
                        else
                        {
                            af.FailureCount++;
                            af.CumulativeReward += config.PenaltyPerActionFail;
                        }
                        int total = af.CompletionCount + af.FailureCount;
                        af.AverageReward = total > 0 ? af.CumulativeReward / total : 0f;
                        actionFeedbacks[afIdx] = af;
                    }

                    if (record.WasSuccessful)
                    {
                        feedback.ValueRW.ActionsCompleted++;
                        feedback.ValueRW.CumulativeScore += config.RewardPerActionComplete;
                    }
                    else
                    {
                        feedback.ValueRW.ActionsFailed++;
                        feedback.ValueRW.CumulativeScore += config.PenaltyPerActionFail;
                    }
                }
                completionRecords.Clear();

                if (elapsedTime - feedback.ValueRO.LastEvaluationTime >= config.EvaluationInterval)
                {
                    float totalSatisfaction = 0f;
                    if (needs.Length > 0)
                    {
                        for (int i = 0; i < needs.Length; i++)
                            totalSatisfaction += 1f - ((float)needs[i].CurrentUrgency / (float)NeedUrgency.Critical);
                        totalSatisfaction /= needs.Length;
                    }

                    float decay = config.SatisfactionDecayRate;
                    feedback.ValueRW.AverageNeedSatisfaction =
                        feedback.ValueRO.AverageNeedSatisfaction * decay + totalSatisfaction * (1f - decay);

                    feedback.ValueRW.LastEvaluationTime = elapsedTime;
                    ecb.SetComponentEnabled<FeedbackEvaluated>(entity, true);

                    float overallPerformance = feedback.ValueRO.AverageNeedSatisfaction * config.NeedSatisfactionWeight;
                    int totalActions = feedback.ValueRO.ActionsCompleted + feedback.ValueRO.ActionsFailed;
                    if (totalActions > 0)
                    {
                        float successRate = (float)feedback.ValueRO.ActionsCompleted / totalActions;
                        overallPerformance += successRate * (1f - config.NeedSatisfactionWeight);
                    }

                    if (overallPerformance < config.UnderperformingThreshold && totalActions > config.MinActionsForEvaluation)
                        ecb.SetComponentEnabled<StrategyUnderperforming>(entity, true);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        static int FindActionFeedback(in DynamicBuffer<ActionFeedbackElement> feedbacks, int actionId)
        {
            for (int i = 0; i < feedbacks.Length; i++)
            {
                if (feedbacks[i].ActionId == actionId)
                    return i;
            }
            return -1;
        }
    }
}
