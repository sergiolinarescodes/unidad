using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Metadata for a resource that behaves as a need. Stored as a buffer on the agent entity.
    /// Each element maps to a ResourceElement by ResourceId — needs do NOT store the actual value.
    /// DecayRate is units-per-second lost. Thresholds define urgency levels.
    /// </summary>
    public struct NeedElement : IBufferElementData
    {
        public int ResourceId;
        public float DecayRate;
        public float CriticalThreshold;
        public float LowThreshold;
        public float HighThreshold;
        public NeedUrgency CurrentUrgency;
    }

    public enum NeedUrgency : byte
    {
        Satisfied = 0,
        Normal = 1,
        Low = 2,
        Critical = 3
    }

    /// <summary>
    /// Per-agent decay rate modifiers. Allows buffs/debuffs on specific need decay.
    /// Uses the existing ModifierElement system.
    /// </summary>
    public struct NeedDecayModifier : IBufferElementData
    {
        public int ResourceId;
        public ModifierElement Modifier;
    }

    /// <summary>1-frame event: an agent's need crossed an urgency threshold.</summary>
    public struct NeedUrgencyChanged : IComponentData, IEnableableComponent { }

    /// <summary>Records which needs changed urgency this frame.</summary>
    public struct NeedUrgencyChangeRecord : IBufferElementData
    {
        public int ResourceId;
        public NeedUrgency OldUrgency;
        public NeedUrgency NewUrgency;
    }
}
