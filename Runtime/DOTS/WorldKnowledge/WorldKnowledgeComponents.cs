using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Point of Interest in the world. Any entity can be a POI by adding this component.
    /// POI type is game-defined (e.g., food source=1, rest area=2, workplace=3).
    /// </summary>
    public struct PointOfInterest : IComponentData
    {
        public int POIType;
        public int Capacity;
        public int CurrentUsers;
        [MarshalAs(UnmanagedType.U1)]
        public bool IsActive;
    }

    /// <summary>Per-agent awareness configuration.</summary>
    public struct AwarenessData : IComponentData
    {
        public float AwarenessRange;
        public float SpatialHashCellSize;
        public int MaxKnownPOIs;
        public int MaxKnownAgents;
    }

    /// <summary>Nearby POI known to the agent. Refreshed by WorldKnowledgeSystem.</summary>
    public struct KnownPOIElement : IBufferElementData
    {
        public Entity POIEntity;
        public int POIType;
        public float3 Position;
        public float Distance;
        public int CurrentUsers;
        public int Capacity;
    }

    /// <summary>Nearby agent known to this agent.</summary>
    public struct KnownAgentElement : IBufferElementData
    {
        public Entity AgentEntity;
        public int ArchetypeId;
        public float3 Position;
        public float Distance;
    }

    /// <summary>
    /// Claim on a POI. Set when an agent begins using a POI.
    /// POIClaimSystem manages claim/release lifecycle.
    /// </summary>
    public struct POIClaim : IComponentData
    {
        public Entity POIEntity;
        public int POIType;
    }

    /// <summary>1-frame: POI claim was rejected (POI at capacity).</summary>
    public struct POIClaimRejected : IComponentData, IEnableableComponent { }

    /// <summary>1-frame: agent's knowledge was refreshed this frame.</summary>
    public struct KnowledgeRefreshed : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Singleton configuration for WorldKnowledgeSystem.
    /// If absent, the system uses defaults (CellSize=10, 3D mode).
    /// Set Is2D=true for games with ground-plane agents to get ~11x fewer cell lookups.
    /// </summary>
    public struct WorldKnowledgeConfig : IComponentData
    {
        public float CellSize;
        [MarshalAs(UnmanagedType.U1)]
        public bool Is2D;

        public static WorldKnowledgeConfig Default => new WorldKnowledgeConfig
        {
            CellSize = 10f,
            Is2D = false
        };
    }
}
