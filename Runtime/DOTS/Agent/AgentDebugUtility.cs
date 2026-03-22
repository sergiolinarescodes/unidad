using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Read-only utility for querying common agent debug state.
    /// Designed for debug overlays and tools — not Burst-compiled (uses strings).
    /// </summary>
    public static class AgentDebugUtility
    {
        public struct AgentSnapshot
        {
            public int AgentId;
            public int ArchetypeId;
            public AgentLifecycleState LifecycleState;
            public AgentActionPhase Phase;
            public int CurrentActionId;
            public int CurrentActionType;
            public AgentActivityType Activity;
            public NavAgentStatus NavStatus;
            public int CurrentNodeId;
            public float BestScore;
            public int BestActionId;
        }

        public static AgentSnapshot GetSnapshot(EntityManager em, Entity agent)
        {
            var snap = new AgentSnapshot();

            if (em.HasComponent<AgentData>(agent))
            {
                var d = em.GetComponentData<AgentData>(agent);
                snap.AgentId = d.AgentId;
                snap.ArchetypeId = d.ArchetypeId;
                snap.LifecycleState = d.LifecycleState;
            }

            if (em.HasComponent<AgentActionState>(agent))
            {
                var a = em.GetComponentData<AgentActionState>(agent);
                snap.Phase = a.Phase;
                snap.CurrentActionId = a.CurrentActionId;
                snap.CurrentActionType = a.CurrentActionType;
            }

            if (em.HasComponent<AgentActivity>(agent))
                snap.Activity = em.GetComponentData<AgentActivity>(agent).CurrentActivity;

            if (em.HasComponent<NavAgent>(agent))
            {
                var n = em.GetComponentData<NavAgent>(agent);
                snap.NavStatus = n.Status;
                snap.CurrentNodeId = n.CurrentNodeId;
            }

            if (em.HasComponent<ScoringResult>(agent))
            {
                var s = em.GetComponentData<ScoringResult>(agent);
                snap.BestScore = s.BestScore;
                snap.BestActionId = s.BestActionId;
            }

            return snap;
        }

        public static string GetPhaseName(AgentActionPhase phase)
        {
            return phase switch
            {
                AgentActionPhase.None => "None",
                AgentActionPhase.Starting => "Starting",
                AgentActionPhase.Navigating => "Navigating",
                AgentActionPhase.Executing => "Executing",
                AgentActionPhase.Completing => "Completing",
                AgentActionPhase.Interrupted => "Interrupted",
                AgentActionPhase.WaitingForCompletion => "WaitingForCompletion",
                _ => $"Phase({(int)phase})"
            };
        }

        public static string GetActivityName(AgentActivityType activity)
        {
            return activity switch
            {
                AgentActivityType.Idle => "Idle",
                AgentActivityType.Moving => "Moving",
                AgentActivityType.PerformingAction => "Performing",
                AgentActivityType.WaitingForInteraction => "Waiting",
                AgentActivityType.Queued => "Queued",
                _ => $"Activity({(int)activity})"
            };
        }

        public static string GetNavStatusName(NavAgentStatus status)
        {
            return status switch
            {
                NavAgentStatus.Idle => "Idle",
                NavAgentStatus.WaitingForPath => "WaitingForPath",
                NavAgentStatus.FollowingPath => "FollowingPath",
                NavAgentStatus.Arrived => "Arrived",
                NavAgentStatus.PathFailed => "PathFailed",
                _ => $"NavStatus({(int)status})"
            };
        }
    }
}
