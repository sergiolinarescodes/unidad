using System.Collections.Generic;

namespace Unidad.Core.Progression
{
    public interface IProgressionService
    {
        void CreateTree(ProgressionTreeId treeId);
        bool HasTree(ProgressionTreeId treeId);
        void AddNode(ProgressionTreeId treeId, ProgressionNodeDefinition definition);
        bool HasNode(ProgressionTreeId treeId, ProgressionNodeId nodeId);
        ProgressionNodeStatus GetStatus(ProgressionTreeId treeId, ProgressionNodeId nodeId);
        IReadOnlyList<ProgressionNodeId> GetAvailableNodes(ProgressionTreeId treeId);
        IReadOnlyList<ProgressionNodeId> GetUnlockedNodes(ProgressionTreeId treeId);
        IReadOnlyList<ProgressionNodeId> GetLockedNodes(ProgressionTreeId treeId);
        IReadOnlyList<ProgressionNodeId> GetAllNodes(ProgressionTreeId treeId);
        ProgressionNodeDefinition GetNodeDefinition(ProgressionTreeId treeId, ProgressionNodeId nodeId);
        bool ArePrerequisitesMet(ProgressionTreeId treeId, ProgressionNodeId nodeId);
        bool TryUnlock(ProgressionTreeId treeId, ProgressionNodeId nodeId);
        void ForceUnlock(ProgressionTreeId treeId, ProgressionNodeId nodeId);
        void Relock(ProgressionTreeId treeId, ProgressionNodeId nodeId);
        void ResetTree(ProgressionTreeId treeId);
    }
}
