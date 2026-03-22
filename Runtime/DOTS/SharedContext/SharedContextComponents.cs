using System.Runtime.InteropServices;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    // === Shared Context Definition Entities ===

    /// <summary>
    /// Scope definition for a shared context entity. Create one global (ArchetypeId = -1)
    /// and optionally one per agent archetype for filtered context.
    /// </summary>
    public struct SharedContextData : IComponentData
    {
        public int ScopeId;
        public int ArchetypeId;
    }

    /// <summary>
    /// Key-value pair on a shared context entity. Game code sets these each frame
    /// (e.g., total wood = 500, threat level = 0.7).
    /// Keys must be contiguous integers starting from 0 for global broadcast efficiency.
    /// </summary>
    public struct SharedContextEntry : IBufferElementData
    {
        public int Key;
        public float Value;
        public double LastUpdatedTime;
    }

    /// <summary>
    /// Defines which keys an archetype can access from shared context.
    /// Stored on shared context definition entities.
    /// </summary>
    public struct ContextAccessRule : IBufferElementData
    {
        public int ArchetypeId;
        public int Key;
        public ContextAccessLevel Access;
    }

    public enum ContextAccessLevel : byte
    {
        None = 0,
        Read = 1,
        ReadWrite = 2
    }

    // === Broadcast Singleton ===

    /// <summary>
    /// Configuration for the global broadcast array. Place on a singleton entity.
    /// MaxKeys determines the size of the NativeArray — keys must be in [0..MaxKeys).
    /// </summary>
    public struct SharedContextBroadcastConfig : IComponentData
    {
        public int MaxKeys;
    }

    // === Per-Agent Components ===

    /// <summary>
    /// Per-agent refresh policy. Configures when the agent's context snapshot
    /// is refreshed from shared context entities.
    /// </summary>
    public struct ContextRefreshPolicy : IComponentData
    {
        public ContextRefreshMode Mode;
        public float RefreshInterval;
        public double LastRefreshTime;
    }

    public enum ContextRefreshMode : byte
    {
        /// <summary>Agent reads from broadcast NativeArray directly. No snapshot needed.</summary>
        EveryFrame = 0,
        /// <summary>Snapshot refreshed every RefreshInterval seconds.</summary>
        Interval = 1,
        /// <summary>Snapshot refreshed only when ScoringSystem runs for this agent.</summary>
        OnScoring = 2,
        /// <summary>Snapshot refreshed on milestone events (ActionCompleted, StateEntered, NeedUrgencyChanged).</summary>
        OnMilestone = 3,
        /// <summary>Snapshot refreshed only when ContextRefreshRequest is enabled by game code.</summary>
        Manual = 4
    }

    /// <summary>Enable to force a context snapshot refresh next frame.</summary>
    public struct ContextRefreshRequest : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Per-agent filtered view of shared context. Populated by SharedContextRefreshSystem
    /// for agents that need archetype-specific keys (not just global broadcast).
    /// </summary>
    public struct AgentContextSnapshot : IBufferElementData
    {
        public int Key;
        public float Value;
    }

    /// <summary>1-frame event: agent's context snapshot was refreshed.</summary>
    public struct ContextRefreshed : IComponentData, IEnableableComponent { }
}
