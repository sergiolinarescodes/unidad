using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Attached to an entity that owns a progression tree.
    /// The tree's nodes are stored in DynamicBuffer&lt;ProgressionNodeElement&gt;.
    /// </summary>
    public struct ProgressionTreeData : IComponentData
    {
        /// <summary>
        /// Application-level tree identifier (maps to managed ProgressionTreeId).
        /// </summary>
        public int TreeId;
    }

    /// <summary>
    /// One node in a progression tree. Stored as a DynamicBuffer on the tree entity.
    /// Nodes reference each other by int NodeId (index into this buffer is NOT the NodeId —
    /// use linear scan or a sorted buffer + binary search).
    /// </summary>
    public struct ProgressionNodeElement : IBufferElementData
    {
        public int NodeId;
        public ProgressionNodeStatus Status;

        /// <summary>
        /// Number of resource costs for this node, stored contiguously in NodeCostElement buffer
        /// starting at CostStartIndex.
        /// </summary>
        public int CostStartIndex;
        public int CostCount;
    }

    /// <summary>
    /// Prerequisite relationship: NodeId requires PrerequisiteNodeId to be Unlocked.
    /// Stored as a DynamicBuffer on the tree entity.
    /// </summary>
    public struct PrerequisiteElement : IBufferElementData
    {
        public int NodeId;
        public int PrerequisiteNodeId;
    }

    /// <summary>
    /// Resource cost for unlocking a node. Stored as a DynamicBuffer on the tree entity.
    /// Indexed by ProgressionNodeElement.CostStartIndex + CostCount.
    /// </summary>
    public struct NodeCostElement : IBufferElementData
    {
        public int ResourceId;
        public float Amount;
    }

    public enum ProgressionNodeStatus : byte
    {
        Locked = 0,
        Available = 1,
        Unlocked = 2
    }

    // --- Request components ---

    /// <summary>
    /// Enable on a tree entity to request unlocking a specific node.
    /// Set UnlockNodeId before enabling.
    /// </summary>
    public struct UnlockRequest : IComponentData, IEnableableComponent
    {
        public int NodeId;
        /// <summary>If true, bypasses prerequisite and cost checks.</summary>
        public bool Force;
    }

    /// <summary>
    /// Enable on a tree entity to request relocking a specific node (with cascade).
    /// </summary>
    public struct RelockRequest : IComponentData, IEnableableComponent
    {
        public int NodeId;
    }

    /// <summary>
    /// Enable on a tree entity to reset all nodes to Locked/Available.
    /// </summary>
    public struct ResetTreeRequest : IComponentData, IEnableableComponent { }

    // --- 1-frame event tags ---

    /// <summary>1-frame: a node was just unlocked.</summary>
    public struct NodeUnlocked : IComponentData, IEnableableComponent { }

    /// <summary>1-frame: a node just became available (prerequisites met).</summary>
    public struct NodeBecameAvailable : IComponentData, IEnableableComponent { }

    /// <summary>1-frame: a node was just relocked.</summary>
    public struct NodeRelocked : IComponentData, IEnableableComponent { }

    /// <summary>1-frame: the tree was just reset.</summary>
    public struct TreeReset : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Buffer recording which nodes changed status this frame.
    /// Cleared by ProgressionSystem at the start of each update.
    /// </summary>
    public struct ProgressionChangeRecord : IBufferElementData
    {
        public int NodeId;
        public ProgressionNodeStatus OldStatus;
        public ProgressionNodeStatus NewStatus;
    }
}
