using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Observable agent state — what is this agent doing right now, and for how long.
    /// Updated by AgentActivitySystem from AgentActionState, NavAgent, and ActionQueueProgress.
    /// Game code / UI reads this to display status (e.g., "Chopping Wood 2.1s / 5.0s").
    /// </summary>
    public struct AgentActivity : IComponentData
    {
        public AgentActivityType CurrentActivity;
        public int CurrentActionId;
        public int CurrentActionType;
        public float ActionDuration;
        public float ActionElapsed;
        public float ActionProgress;
        public float3 ActivityTargetPosition;
        public Entity ActivityTargetEntity;
    }

    public enum AgentActivityType : byte
    {
        Idle = 0,
        Moving = 1,
        PerformingAction = 2,
        WaitingForInteraction = 3,
        Queued = 4
    }

    /// <summary>1-frame event: agent's activity changed this frame.</summary>
    public struct ActivityChanged : IComponentData, IEnableableComponent { }
}
