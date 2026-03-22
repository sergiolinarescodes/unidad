using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    // === Strategy Definition Entity ===

    /// <summary>
    /// Strategy definition. One entity per strategy set, referenced by agents via AgentData.StrategyId.
    /// </summary>
    public struct StrategyDefinition : IComponentData
    {
        public int StrategyId;
        public FixedString64Bytes DebugName;
    }

    /// <summary>
    /// One action defined in a strategy. PreconditionFlags is a bitmask checked against
    /// AgentPreconditions.AvailableFlags — all bits must be set for the action to be considered.
    /// </summary>
    public struct StrategyActionElement : IBufferElementData
    {
        public int ActionId;
        public int ActionType;
        public float PriorityBonus;
        public float Cooldown;
        public int PreconditionFlags;
    }

    /// <summary>
    /// Template consideration on the strategy definition entity.
    /// Copied to the agent's ConsiderationElement buffer on strategy assignment.
    /// </summary>
    public struct StrategyConsiderationTemplate : IBufferElementData
    {
        public int ActionId;
        public ScoringInputType InputType;
        public int InputParam;
        public ResponseCurveType CurveType;
        public float CurveA;
        public float CurveB;
        public float CurveC;
        public float CurveD;
    }

    /// <summary>
    /// Template for action effects on the strategy definition entity.
    /// Describes what happens when an action completes (need restoration, state trigger, etc.).
    /// </summary>
    public struct StrategyActionEffectTemplate : IBufferElementData
    {
        public int ActionId;
        public ActionEffectType EffectType;
        public int TargetResourceId;
        public float Value;
    }

    public enum ActionEffectType : byte
    {
        AddToResource = 0,
        SetResource = 1,
        SetNeedUrgency = 2,
        TriggerState = 3,
        SpawnTimer = 4
        // Future effect types added here (e.g., AddMemory, ModifyRelationship)
    }

    /// <summary>
    /// Template for multi-step action plans on the strategy definition entity.
    /// When an action with a plan is selected, these entries are copied to the agent's ActionQueueEntry.
    /// </summary>
    public struct StrategyActionPlanEntry : IBufferElementData
    {
        public int ActionId;
        public int StepIndex;
        public int StepActionType;
        public float3 StepTargetOffset;
        public float StepDuration;
        public int StepIntParam;
    }

    // === Per-Agent Strategy Components ===

    /// <summary>
    /// Per-agent tunable parameter. Strategies inject default values on assignment.
    /// Considerations can read these via ScoringInputType.StrategyParam.
    /// </summary>
    public struct StrategyParamElement : IBufferElementData
    {
        public int ParamId;
        public float Value;
    }

    /// <summary>
    /// Enable on an agent entity to request strategy reassignment.
    /// Set StrategyId before enabling. StrategyAssignmentSystem processes it.
    /// </summary>
    public struct StrategyAssignRequest : IComponentData, IEnableableComponent
    {
        public int StrategyId;
    }

    /// <summary>1-frame event: strategy was reassigned this frame.</summary>
    public struct StrategyAssigned : IComponentData, IEnableableComponent { }
}
