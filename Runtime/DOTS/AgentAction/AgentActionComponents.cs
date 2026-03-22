using System.Runtime.InteropServices;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Agent's currently executing action. Links to ScoringResult and CommandQueue.
    /// Phase describes where in the execution pipeline the agent is.
    /// </summary>
    public struct AgentActionState : IComponentData
    {
        public int CurrentActionId;
        public int CurrentActionType;
        public AgentActionPhase Phase;
        public float ActionStartTime;
    }

    public enum AgentActionPhase : byte
    {
        None = 0,
        Starting = 1,
        Navigating = 2,
        Executing = 3,
        Completing = 4,
        Interrupted = 5,
        WaitingForCompletion = 6
    }

    /// <summary>
    /// Per-agent buffer storing effects to apply when the current action completes.
    /// Populated from StrategyActionEffectTemplate when an action begins.
    /// </summary>
    public struct ActionEffectElement : IBufferElementData
    {
        public ActionEffectType EffectType;
        public int TargetResourceId;
        public float Value;
    }

    /// <summary>
    /// Precondition flags currently available to the agent.
    /// Updated by game-specific systems (e.g., "at home" flag, "has tool" flag).
    /// </summary>
    public struct AgentPreconditions : IComponentData
    {
        public int AvailableFlags;
    }

    // --- Action events (1-frame) ---
    public struct ActionStarted : IComponentData, IEnableableComponent { }
    public struct ActionCompleted : IComponentData, IEnableableComponent { }
    public struct ActionInterrupted : IComponentData, IEnableableComponent { }

    /// <summary>Records action completions for feedback tracking.</summary>
    public struct ActionCompletionRecord : IBufferElementData
    {
        public int ActionId;
        public int ActionType;
        public double CompletedTime;
        [MarshalAs(UnmanagedType.U1)]
        public bool WasSuccessful;
    }
}
