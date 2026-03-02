using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;

namespace Unidad.Core.Progression
{
    internal sealed class ProgressionService : SystemServiceBase, IProgressionService
    {
        private readonly Dictionary<string, TreeEntry> _trees = new();

        public ProgressionService(IEventBus eventBus) : base(eventBus) { }

        public void CreateTree(ProgressionTreeId treeId)
        {
            if (_trees.ContainsKey(treeId.Value))
                throw new InvalidOperationException($"Tree '{treeId.Value}' already exists.");

            _trees[treeId.Value] = new TreeEntry();
            Publish(new TreeCreatedEvent(treeId));
        }

        public bool HasTree(ProgressionTreeId treeId) => _trees.ContainsKey(treeId.Value);

        public void AddNode(ProgressionTreeId treeId, ProgressionNodeDefinition definition)
        {
            var tree = GetTree(treeId);
            if (tree.Nodes.ContainsKey(definition.Id.Value))
                throw new InvalidOperationException($"Node '{definition.Id.Value}' already exists in tree '{treeId.Value}'.");

            var status = ArePrerequisitesMetInternal(tree, definition)
                ? ProgressionNodeStatus.Available
                : ProgressionNodeStatus.Locked;

            tree.Nodes[definition.Id.Value] = new NodeEntry(definition, status);

            if (status == ProgressionNodeStatus.Available)
                Publish(new NodeBecameAvailableEvent(treeId, definition.Id));
        }

        public bool HasNode(ProgressionTreeId treeId, ProgressionNodeId nodeId)
        {
            return GetTree(treeId).Nodes.ContainsKey(nodeId.Value);
        }

        public ProgressionNodeStatus GetStatus(ProgressionTreeId treeId, ProgressionNodeId nodeId)
        {
            return GetNode(treeId, nodeId).Status;
        }

        public IReadOnlyList<ProgressionNodeId> GetAvailableNodes(ProgressionTreeId treeId)
        {
            return GetNodesByStatus(treeId, ProgressionNodeStatus.Available);
        }

        public IReadOnlyList<ProgressionNodeId> GetUnlockedNodes(ProgressionTreeId treeId)
        {
            return GetNodesByStatus(treeId, ProgressionNodeStatus.Unlocked);
        }

        public IReadOnlyList<ProgressionNodeId> GetLockedNodes(ProgressionTreeId treeId)
        {
            return GetNodesByStatus(treeId, ProgressionNodeStatus.Locked);
        }

        public IReadOnlyList<ProgressionNodeId> GetAllNodes(ProgressionTreeId treeId)
        {
            var tree = GetTree(treeId);
            var result = new List<ProgressionNodeId>(tree.Nodes.Count);
            foreach (var kvp in tree.Nodes)
                result.Add(new ProgressionNodeId(kvp.Key));
            return result;
        }

        public ProgressionNodeDefinition GetNodeDefinition(ProgressionTreeId treeId, ProgressionNodeId nodeId)
        {
            return GetNode(treeId, nodeId).Definition;
        }

        public bool ArePrerequisitesMet(ProgressionTreeId treeId, ProgressionNodeId nodeId)
        {
            var tree = GetTree(treeId);
            var node = GetNodeFromTree(tree, nodeId);
            return ArePrerequisitesMetInternal(tree, node.Definition);
        }

        public bool TryUnlock(ProgressionTreeId treeId, ProgressionNodeId nodeId)
        {
            var tree = GetTree(treeId);
            var node = GetNodeFromTree(tree, nodeId);

            if (node.Status == ProgressionNodeStatus.Unlocked) return true;
            if (!ArePrerequisitesMetInternal(tree, node.Definition)) return false;

            node.Status = ProgressionNodeStatus.Unlocked;
            Publish(new NodeUnlockedEvent(treeId, nodeId));

            // Check for newly available nodes
            ScanForNewlyAvailable(treeId, tree);
            return true;
        }

        public void ForceUnlock(ProgressionTreeId treeId, ProgressionNodeId nodeId)
        {
            var tree = GetTree(treeId);
            var node = GetNodeFromTree(tree, nodeId);

            if (node.Status == ProgressionNodeStatus.Unlocked) return;

            node.Status = ProgressionNodeStatus.Unlocked;
            Publish(new NodeUnlockedEvent(treeId, nodeId));

            ScanForNewlyAvailable(treeId, tree);
        }

        public void Relock(ProgressionTreeId treeId, ProgressionNodeId nodeId)
        {
            var tree = GetTree(treeId);
            var node = GetNodeFromTree(tree, nodeId);

            if (node.Status == ProgressionNodeStatus.Locked) return;

            node.Status = ProgressionNodeStatus.Locked;
            Publish(new NodeRelockedEvent(treeId, nodeId));

            // Cascade: relock any node that requires this one
            CascadeRelock(treeId, tree, nodeId);

            // Rescan availability after cascade
            ScanForNewlyAvailable(treeId, tree);
        }

        public void ResetTree(ProgressionTreeId treeId)
        {
            var tree = GetTree(treeId);

            foreach (var kvp in tree.Nodes)
            {
                kvp.Value.Status = ProgressionNodeStatus.Locked;
            }

            // Recalculate availability for nodes with no prerequisites
            foreach (var kvp in tree.Nodes)
            {
                if (ArePrerequisitesMetInternal(tree, kvp.Value.Definition))
                {
                    kvp.Value.Status = ProgressionNodeStatus.Available;
                }
            }

            Publish(new TreeResetEvent(treeId));
        }

        private TreeEntry GetTree(ProgressionTreeId treeId)
        {
            if (!_trees.TryGetValue(treeId.Value, out var tree))
                throw new KeyNotFoundException($"Tree '{treeId.Value}' does not exist.");
            return tree;
        }

        private NodeEntry GetNode(ProgressionTreeId treeId, ProgressionNodeId nodeId)
        {
            var tree = GetTree(treeId);
            return GetNodeFromTree(tree, nodeId);
        }

        private static NodeEntry GetNodeFromTree(TreeEntry tree, ProgressionNodeId nodeId)
        {
            if (!tree.Nodes.TryGetValue(nodeId.Value, out var node))
                throw new KeyNotFoundException($"Node '{nodeId.Value}' does not exist.");
            return node;
        }

        private static bool ArePrerequisitesMetInternal(TreeEntry tree, ProgressionNodeDefinition definition)
        {
            if (definition.Prerequisites == null || definition.Prerequisites.Count == 0)
                return true;

            foreach (var prereq in definition.Prerequisites)
            {
                if (!tree.Nodes.TryGetValue(prereq.Value, out var prereqNode))
                    return false;
                if (prereqNode.Status != ProgressionNodeStatus.Unlocked)
                    return false;
            }
            return true;
        }

        private List<ProgressionNodeId> GetNodesByStatus(ProgressionTreeId treeId, ProgressionNodeStatus status)
        {
            var tree = GetTree(treeId);
            var result = new List<ProgressionNodeId>();
            foreach (var kvp in tree.Nodes)
            {
                if (kvp.Value.Status == status)
                    result.Add(new ProgressionNodeId(kvp.Key));
            }
            return result;
        }

        private void ScanForNewlyAvailable(ProgressionTreeId treeId, TreeEntry tree)
        {
            foreach (var kvp in tree.Nodes)
            {
                if (kvp.Value.Status != ProgressionNodeStatus.Locked) continue;

                if (ArePrerequisitesMetInternal(tree, kvp.Value.Definition))
                {
                    kvp.Value.Status = ProgressionNodeStatus.Available;
                    Publish(new NodeBecameAvailableEvent(treeId, new ProgressionNodeId(kvp.Key)));
                }
            }
        }

        private void CascadeRelock(ProgressionTreeId treeId, TreeEntry tree, ProgressionNodeId relockedId)
        {
            foreach (var kvp in tree.Nodes)
            {
                if (kvp.Value.Status == ProgressionNodeStatus.Locked) continue;

                var def = kvp.Value.Definition;
                if (def.Prerequisites == null) continue;

                foreach (var prereq in def.Prerequisites)
                {
                    if (prereq == relockedId)
                    {
                        var childId = new ProgressionNodeId(kvp.Key);
                        if (kvp.Value.Status != ProgressionNodeStatus.Locked)
                        {
                            kvp.Value.Status = ProgressionNodeStatus.Locked;
                            Publish(new NodeRelockedEvent(treeId, childId));
                            // Recursive cascade
                            CascadeRelock(treeId, tree, childId);
                        }
                        break;
                    }
                }
            }
        }

        private sealed class TreeEntry
        {
            public readonly Dictionary<string, NodeEntry> Nodes = new();
        }

        private sealed class NodeEntry
        {
            public readonly ProgressionNodeDefinition Definition;
            public ProgressionNodeStatus Status;

            public NodeEntry(ProgressionNodeDefinition definition, ProgressionNodeStatus status)
            {
                Definition = definition;
                Status = status;
            }
        }
    }
}
