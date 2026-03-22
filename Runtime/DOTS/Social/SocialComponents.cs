using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Enable on an agent to request an interaction with TargetAgent.
    /// InteractionRequestSystem processes these.
    /// </summary>
    public struct InteractionRequest : IComponentData, IEnableableComponent
    {
        public Entity TargetAgent;
        public int InteractionType;
        public int OfferParam;
        public float OfferValue;
    }

    /// <summary>
    /// Set on the target agent when a request is pending.
    /// </summary>
    public struct InteractionResponse : IComponentData, IEnableableComponent
    {
        public Entity RequestingAgent;
        public int InteractionType;
        public InteractionResponseType Response;
        public float CounterValue;
    }

    public enum InteractionResponseType : byte
    {
        Accept = 0,
        Reject = 1,
        Counter = 2
    }

    /// <summary>
    /// Current interaction state for an agent.
    /// </summary>
    public struct InteractionState : IComponentData
    {
        public Entity PartnerEntity;
        public int InteractionType;
        public InteractionPhase Phase;
    }

    public enum InteractionPhase : byte
    {
        None = 0,
        Requested = 1,
        Active = 2,
        Completing = 3
    }

    /// <summary>
    /// Per-agent relationship memory. Tracks trust and familiarity with other agents.
    /// </summary>
    public struct RelationshipElement : IBufferElementData
    {
        public int TargetAgentId;
        public float Trust;
        public float Familiarity;
        public int InteractionCount;
        public double LastInteractionTime;
    }

    // --- Interaction events (1-frame) ---
    public struct InteractionStarted : IComponentData, IEnableableComponent { }
    public struct InteractionCompleted : IComponentData, IEnableableComponent { }
    public struct InteractionRejected : IComponentData, IEnableableComponent { }
}
