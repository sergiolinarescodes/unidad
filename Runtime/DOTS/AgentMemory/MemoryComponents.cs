using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Per-agent memory configuration.
    /// </summary>
    public struct MemoryConfig : IComponentData
    {
        public int MaxMemories;
        public float DecayRate;
        public float ImportanceThreshold;

        public static MemoryConfig Default => new MemoryConfig
        {
            MaxMemories = 32,
            DecayRate = 0.01f,
            ImportanceThreshold = 0.05f
        };
    }

    /// <summary>
    /// One episodic memory. Importance decays over time; forgotten when below threshold.
    /// </summary>
    public struct MemoryElement : IBufferElementData
    {
        public int MemoryType;
        public float3 Location;
        public double Timestamp;
        public float Importance;
        public int IntParam;
        public float FloatParam;
    }

    /// <summary>1-frame: a memory was added this frame.</summary>
    public struct MemoryAdded : IComponentData, IEnableableComponent { }

    /// <summary>1-frame: one or more memories were forgotten this frame.</summary>
    public struct MemoryForgotten : IComponentData, IEnableableComponent { }
}
