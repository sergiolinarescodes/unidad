using System.Runtime.InteropServices;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Response curve type for mapping input [0..1] to output [0..1].
    /// All curves use the same 4 parameters (A, B, C, D) interpreted per type.
    /// </summary>
    public enum ResponseCurveType : byte
    {
        Linear,       // y = A*x + B, clamped [0..1]
        Quadratic,    // y = A*(x-B)^2 + C
        Logistic,     // y = 1 / (1 + e^(-A*(x-B)))
        Step,         // y = x >= A ? B : C
        Exponential,  // y = A * e^(B*x) + C
        Inverse       // y = A / (x + B) + C
    }

    /// <summary>
    /// What the scoring system reads as input. InputParam specifies which
    /// specific value within the category.
    /// </summary>
    public enum ScoringInputType : byte
    {
        Constant,           // Always returns InputParam/100f (fixed value)
        NeedLevel,          // Normalized need deficit [0..1] for ResourceId=InputParam
        NeedUrgency,        // Urgency enum as float (0=Satisfied..3=Critical) / 3
        DistanceToTarget,   // Distance to AgentTarget, normalized by InputParam (max range)
        TimeSinceAction,    // Seconds since action InputParam last completed
        ResourceLevel,      // Current resource value / max for ResourceId=InputParam
        NearbyPOICount,     // Count of POIs of type InputParam in awareness range, normalized
        AgentState,         // 1.0 if current StateMachine state == InputParam, else 0.0
        WorldTime,          // Time-of-day normalized [0..1] (InputParam unused)
        StrategyParam,      // Reads StrategyParamElement with ParamId=InputParam
        SharedContext,      // Reads from broadcast NativeArray by Key=InputParam
        AgentContext,       // Reads from AgentContextSnapshot by Key=InputParam
        Random              // Random [0..1] per evaluation (seeded by entity+frame)
    }

    /// <summary>
    /// One input consideration for an action. Multiple considerations are multiplied together
    /// (geometric mean compensated) to produce the action's final score.
    /// Considerations MUST be sorted by ActionId in the buffer for contiguous-run processing.
    /// </summary>
    public struct ConsiderationElement : IBufferElementData
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
    /// Result of scoring all actions for an agent. Written by ScoringSystem.
    /// </summary>
    public struct ScoringResult : IComponentData
    {
        public int BestActionId;
        public float BestScore;
        public int PreviousBestActionId;
        [MarshalAs(UnmanagedType.U1)]
        public bool ActionChanged;
    }

    /// <summary>
    /// Tracks when an action was last performed, enabling time-based considerations.
    /// </summary>
    public struct ActionTimestampElement : IBufferElementData
    {
        public int ActionId;
        public double LastCompletedTime;
    }

    /// <summary>1-frame event: the best action changed this frame.</summary>
    public struct ActionSelectionChanged : IComponentData, IEnableableComponent { }
}
