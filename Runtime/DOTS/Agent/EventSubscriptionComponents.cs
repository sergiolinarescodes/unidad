using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Per-agent subscription: when EventType fires, apply Reaction.
    /// EventParam filters (e.g., specific ResourceId, POI type, custom event ID).
    /// </summary>
    public struct EventSubscriptionElement : IBufferElementData
    {
        public AgentEventType EventType;
        public AgentReaction Reaction;
        public int EventParam;
    }

    public enum AgentEventType : byte
    {
        ResourceDepleted = 0,
        ResourceChanged = 1,
        NeedBecameCritical = 2,
        NearbyPOIAppeared = 3,
        NearbyAgentAppeared = 4,
        NavPathInvalidated = 5,
        ActionCompleted = 6,
        ActionFailed = 7,
        SharedContextChanged = 8,
        ScheduleSlotChanged = 9,
        Custom = 10
    }

    public enum AgentReaction : byte
    {
        RefreshContext = 0,
        ForceRescore = 1,
        RequestPath = 2,
        InterruptAction = 3,
        ForceInterrupt = 4,
        ClearQueue = 5,
        PauseQueue = 6,
        ResumeQueue = 7,
        TransitionState = 8,
        EnqueueAction = 9,
        AddMemory = 10,
        Custom = 11
    }

    /// <summary>
    /// Tag to force rescoring on the next frame, regardless of AllowRescore setting.
    /// Set by EventSubscriptionSystem when a ForceRescore reaction fires.
    /// </summary>
    public struct ForceRescoreTag : IComponentData, IEnableableComponent { }
}
