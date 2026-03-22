using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Utility for manual queue manipulation. Used by game code in QueueManual mode.
    /// </summary>
    public static class ActionQueueUtility
    {
        /// <summary>
        /// Enqueue an action at the end of the queue.
        /// </summary>
        public static void Enqueue(ref DynamicBuffer<ActionQueueEntry> queue,
            ref ActionQueueProgress progress, int actionId, int actionType,
            float3 targetPosition = default, Entity targetEntity = default)
        {
            int index = queue.Length;
            queue.Add(new ActionQueueEntry
            {
                ActionId = actionId,
                ActionType = actionType,
                SequenceIndex = index,
                TargetPosition = targetPosition,
                TargetEntity = targetEntity,
                Status = ActionQueueEntryStatus.Pending
            });
            progress.TotalEntries = queue.Length;
        }

        /// <summary>
        /// Clear the queue and reset progress.
        /// </summary>
        public static void ClearQueue(ref DynamicBuffer<ActionQueueEntry> queue,
            ref ActionQueueProgress progress)
        {
            queue.Clear();
            progress.CurrentIndex = 0;
            progress.TotalEntries = 0;
            progress.QueuePaused = false;
        }

        /// <summary>
        /// Populate the queue from a strategy's plan entries for a given action.
        /// </summary>
        public static void PopulateFromPlan(
            ref DynamicBuffer<ActionQueueEntry> queue,
            ref ActionQueueProgress progress,
            in DynamicBuffer<StrategyActionPlanEntry> planEntries,
            int actionId, float3 baseTargetPosition)
        {
            queue.Clear();
            int sequenceIndex = 0;

            for (int i = 0; i < planEntries.Length; i++)
            {
                if (planEntries[i].ActionId != actionId)
                    continue;

                queue.Add(new ActionQueueEntry
                {
                    ActionId = actionId,
                    ActionType = planEntries[i].StepActionType,
                    SequenceIndex = sequenceIndex++,
                    TargetPosition = baseTargetPosition + planEntries[i].StepTargetOffset,
                    TargetEntity = Entity.Null,
                    Status = ActionQueueEntryStatus.Pending
                });
            }

            progress.CurrentIndex = 0;
            progress.TotalEntries = queue.Length;
            progress.QueuePaused = false;
        }
    }
}
