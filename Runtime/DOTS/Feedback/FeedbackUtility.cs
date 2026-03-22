using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class FeedbackUtility
    {
        public static float GetActionSuccessRate(
            in DynamicBuffer<ActionFeedbackElement> feedbacks, int actionId)
        {
            for (int i = 0; i < feedbacks.Length; i++)
            {
                if (feedbacks[i].ActionId == actionId)
                {
                    int total = feedbacks[i].CompletionCount + feedbacks[i].FailureCount;
                    return total > 0 ? (float)feedbacks[i].CompletionCount / total : 0f;
                }
            }
            return 0f;
        }

        public static float GetOverallSuccessRate(in AgentFeedback feedback)
        {
            int total = feedback.ActionsCompleted + feedback.ActionsFailed;
            return total > 0 ? (float)feedback.ActionsCompleted / total : 0f;
        }

        public static void ResetFeedback(ref AgentFeedback feedback,
            ref DynamicBuffer<ActionFeedbackElement> actionFeedbacks)
        {
            feedback.CumulativeScore = 0f;
            feedback.ActionsCompleted = 0;
            feedback.ActionsFailed = 0;
            feedback.AverageNeedSatisfaction = 0.5f;
            feedback.LastEvaluationTime = 0.0;
            actionFeedbacks.Clear();
        }
    }
}
