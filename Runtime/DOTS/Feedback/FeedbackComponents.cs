using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Accumulated performance metrics for an agent's strategy.
    /// Used to evaluate how well a strategy is working.
    /// </summary>
    public struct AgentFeedback : IComponentData
    {
        public float CumulativeScore;
        public int ActionsCompleted;
        public int ActionsFailed;
        public float AverageNeedSatisfaction;
        public double LastEvaluationTime;
    }

    /// <summary>Per-action performance record.</summary>
    public struct ActionFeedbackElement : IBufferElementData
    {
        public int ActionId;
        public int CompletionCount;
        public int FailureCount;
        public float CumulativeReward;
        public float AverageReward;
    }

    /// <summary>Configuration for the feedback evaluation system. Singleton.</summary>
    public struct FeedbackConfig : IComponentData
    {
        public float EvaluationInterval;
        public float SatisfactionDecayRate;
        public float RewardPerActionComplete;
        public float PenaltyPerActionFail;
        public float NeedSatisfactionWeight;
        public float UnderperformingThreshold;
        public int MinActionsForEvaluation;

        public static FeedbackConfig Default => new FeedbackConfig
        {
            EvaluationInterval = 5f,
            SatisfactionDecayRate = 0.9f,
            RewardPerActionComplete = 1f,
            PenaltyPerActionFail = -0.5f,
            NeedSatisfactionWeight = 0.6f,
            UnderperformingThreshold = 0.3f,
            MinActionsForEvaluation = 5
        };
    }

    /// <summary>1-frame: feedback was recalculated.</summary>
    public struct FeedbackEvaluated : IComponentData, IEnableableComponent { }

    /// <summary>1-frame: strategy is underperforming.</summary>
    public struct StrategyUnderperforming : IComponentData, IEnableableComponent { }
}
