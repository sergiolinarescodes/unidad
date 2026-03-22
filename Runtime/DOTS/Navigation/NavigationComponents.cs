using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    // === Nav Graph Definition Entity ===

    public struct NavGraphData : IComponentData
    {
        public int GraphId;
        public NavGraphType GraphType;
        public int NodeCount;
    }

    public enum NavGraphType : byte
    {
        Freeform = 0,
        Grid = 1
    }

    /// <summary>
    /// A node in the navigation graph. Position in world space.
    /// Flags encode traversability (game-defined bitmask).
    /// </summary>
    public struct NavNodeElement : IBufferElementData
    {
        public int NodeId;
        public float3 WorldPosition;
        public int Flags;
        public float BaseCost;
    }

    /// <summary>
    /// A directed edge in the navigation graph. For bidirectional, add two entries.
    /// </summary>
    public struct NavEdgeElement : IBufferElementData
    {
        public int FromNodeId;
        public int ToNodeId;
        public float Cost;
        public int RequiredFlags;
    }

    // === Per-Agent Navigation State ===

    public struct NavAgent : IComponentData
    {
        public int GraphId;
        public int CurrentNodeId;
        public int CapabilityFlags;
        public NavAgentStatus Status;
    }

    public enum NavAgentStatus : byte
    {
        Idle = 0,
        WaitingForPath = 1,
        FollowingPath = 2,
        Arrived = 3,
        PathFailed = 4
    }

    /// <summary>Enable to request a path. Set TargetNodeId or TargetWorldPosition.</summary>
    public struct PathRequest : IComponentData, IEnableableComponent
    {
        public int TargetNodeId;
        public float3 TargetWorldPosition;
    }

    /// <summary>Computed path nodes. Index 0 = first node to move toward.</summary>
    public struct PathNodeElement : IBufferElementData
    {
        public int NodeId;
        public float3 WorldPosition;
    }

    public struct PathProgress : IComponentData
    {
        public int CurrentPathIndex;
        public int PathLength;
        public float TotalPathCost;
    }

    // --- Navigation events (1-frame) ---
    public struct PathFound : IComponentData, IEnableableComponent { }
    public struct PathNotFound : IComponentData, IEnableableComponent { }
    public struct PathCompleted : IComponentData, IEnableableComponent { }
    public struct NavNodeReached : IComponentData, IEnableableComponent { }
    public struct PathInvalidated : IComponentData, IEnableableComponent { }

    // --- Dynamic graph change tracking ---
    public struct NavGraphChanged : IComponentData, IEnableableComponent { }

    public struct NavGraphChangeRecord : IBufferElementData
    {
        public int NodeId;
        public NavGraphChangeType ChangeType;
    }

    public enum NavGraphChangeType : byte
    {
        NodeAdded = 0,
        NodeRemoved = 1,
        NodeFlagsChanged = 2,
        EdgeCostChanged = 3,
        EdgeAdded = 4,
        EdgeRemoved = 5
    }

    /// <summary>
    /// Singleton configuration for path request throttling.
    /// </summary>
    public struct PathRequestConfig : IComponentData
    {
        public int MaxPathsPerFrame;

        public static PathRequestConfig Default => new PathRequestConfig
        {
            MaxPathsPerFrame = 32
        };
    }
}
