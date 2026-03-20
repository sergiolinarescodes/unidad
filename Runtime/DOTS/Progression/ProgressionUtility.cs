using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class ProgressionUtility
    {
        /// <summary>
        /// Returns the index of the node with the given NodeId, or -1 if not found.
        /// </summary>
        public static int FindNode(in DynamicBuffer<ProgressionNodeElement> nodes, int nodeId)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].NodeId == nodeId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Returns true if all prerequisites for the given nodeId are Unlocked.
        /// </summary>
        public static bool ArePrerequisitesMet(
            in DynamicBuffer<ProgressionNodeElement> nodes,
            in DynamicBuffer<PrerequisiteElement> prereqs,
            int nodeId)
        {
            for (int i = 0; i < prereqs.Length; i++)
            {
                if (prereqs[i].NodeId != nodeId)
                    continue;

                int prereqIdx = FindNode(in nodes, prereqs[i].PrerequisiteNodeId);
                if (prereqIdx < 0)
                    return false;
                if (nodes[prereqIdx].Status != ProgressionNodeStatus.Unlocked)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if the entity has enough resources to cover all costs for a node.
        /// </summary>
        public static bool CanAfford(
            in ProgressionNodeElement node,
            in DynamicBuffer<NodeCostElement> costs,
            in DynamicBuffer<ResourceElement> resources,
            in DynamicBuffer<ResourceMinModifier> minMods)
        {
            for (int i = 0; i < node.CostCount; i++)
            {
                var cost = costs[node.CostStartIndex + i];
                float current = ResourceUtility.Get(in resources, cost.ResourceId);
                float effMin = GetEffectiveMinForResource(cost.ResourceId, in resources, in minMods);
                if (current - cost.Amount < effMin)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Spends resources for a node's costs. Assumes CanAfford was already checked.
        /// </summary>
        public static void SpendCosts(
            in ProgressionNodeElement node,
            in DynamicBuffer<NodeCostElement> costs,
            ref DynamicBuffer<ResourceElement> resources,
            ref DynamicBuffer<ResourceChangeRecord> resourceChanges,
            in DynamicBuffer<ResourceMaxModifier> maxMods,
            in DynamicBuffer<ResourceMinModifier> minMods)
        {
            for (int i = 0; i < node.CostCount; i++)
            {
                var cost = costs[node.CostStartIndex + i];
                ResourceUtility.TrySpend(ref resources, ref resourceChanges,
                    in maxMods, in minMods, cost.ResourceId, cost.Amount);
            }
        }

        /// <summary>
        /// Attempts to unlock a node. Returns true if the node was successfully unlocked.
        /// Does NOT spend resources — caller must handle that.
        /// </summary>
        public static bool TryUnlock(
            ref DynamicBuffer<ProgressionNodeElement> nodes,
            in DynamicBuffer<PrerequisiteElement> prereqs,
            ref DynamicBuffer<ProgressionChangeRecord> changes,
            int nodeId)
        {
            int idx = FindNode(in nodes, nodeId);
            if (idx < 0)
                return false;

            var node = nodes[idx];
            if (node.Status == ProgressionNodeStatus.Unlocked)
                return true;

            if (!ArePrerequisitesMet(in nodes, in prereqs, nodeId))
                return false;

            changes.Add(new ProgressionChangeRecord
            {
                NodeId = nodeId,
                OldStatus = node.Status,
                NewStatus = ProgressionNodeStatus.Unlocked
            });

            node.Status = ProgressionNodeStatus.Unlocked;
            nodes[idx] = node;

            ScanForNewlyAvailable(ref nodes, in prereqs, ref changes);
            return true;
        }

        /// <summary>
        /// Force-unlocks a node, bypassing prerequisite checks.
        /// </summary>
        public static void ForceUnlock(
            ref DynamicBuffer<ProgressionNodeElement> nodes,
            in DynamicBuffer<PrerequisiteElement> prereqs,
            ref DynamicBuffer<ProgressionChangeRecord> changes,
            int nodeId)
        {
            int idx = FindNode(in nodes, nodeId);
            if (idx < 0)
                return;

            var node = nodes[idx];
            if (node.Status == ProgressionNodeStatus.Unlocked)
                return;

            changes.Add(new ProgressionChangeRecord
            {
                NodeId = nodeId,
                OldStatus = node.Status,
                NewStatus = ProgressionNodeStatus.Unlocked
            });

            node.Status = ProgressionNodeStatus.Unlocked;
            nodes[idx] = node;

            ScanForNewlyAvailable(ref nodes, in prereqs, ref changes);
        }

        /// <summary>
        /// Relocks a node and cascades to any dependents.
        /// </summary>
        public static void Relock(
            ref DynamicBuffer<ProgressionNodeElement> nodes,
            in DynamicBuffer<PrerequisiteElement> prereqs,
            ref DynamicBuffer<ProgressionChangeRecord> changes,
            int nodeId)
        {
            int idx = FindNode(in nodes, nodeId);
            if (idx < 0)
                return;

            var node = nodes[idx];
            if (node.Status == ProgressionNodeStatus.Locked)
                return;

            changes.Add(new ProgressionChangeRecord
            {
                NodeId = nodeId,
                OldStatus = node.Status,
                NewStatus = ProgressionNodeStatus.Locked
            });

            node.Status = ProgressionNodeStatus.Locked;
            nodes[idx] = node;

            CascadeRelock(ref nodes, in prereqs, ref changes, nodeId);
            ScanForNewlyAvailable(ref nodes, in prereqs, ref changes);
        }

        /// <summary>
        /// Resets all nodes to Locked, then recalculates availability for root nodes.
        /// </summary>
        public static void ResetTree(
            ref DynamicBuffer<ProgressionNodeElement> nodes,
            in DynamicBuffer<PrerequisiteElement> prereqs,
            ref DynamicBuffer<ProgressionChangeRecord> changes)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node.Status != ProgressionNodeStatus.Locked)
                {
                    changes.Add(new ProgressionChangeRecord
                    {
                        NodeId = node.NodeId,
                        OldStatus = node.Status,
                        NewStatus = ProgressionNodeStatus.Locked
                    });
                    node.Status = ProgressionNodeStatus.Locked;
                    nodes[i] = node;
                }
            }

            ScanForNewlyAvailable(ref nodes, in prereqs, ref changes);
        }

        /// <summary>
        /// Scans all Locked nodes and promotes to Available if prerequisites are met.
        /// </summary>
        public static void ScanForNewlyAvailable(
            ref DynamicBuffer<ProgressionNodeElement> nodes,
            in DynamicBuffer<PrerequisiteElement> prereqs,
            ref DynamicBuffer<ProgressionChangeRecord> changes)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node.Status != ProgressionNodeStatus.Locked)
                    continue;

                if (ArePrerequisitesMet(in nodes, in prereqs, node.NodeId))
                {
                    changes.Add(new ProgressionChangeRecord
                    {
                        NodeId = node.NodeId,
                        OldStatus = ProgressionNodeStatus.Locked,
                        NewStatus = ProgressionNodeStatus.Available
                    });
                    node.Status = ProgressionNodeStatus.Available;
                    nodes[i] = node;
                }
            }
        }

        static void CascadeRelock(
            ref DynamicBuffer<ProgressionNodeElement> nodes,
            in DynamicBuffer<PrerequisiteElement> prereqs,
            ref DynamicBuffer<ProgressionChangeRecord> changes,
            int relockedNodeId)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node.Status == ProgressionNodeStatus.Locked)
                    continue;

                // Check if this node depends on the relocked node
                bool dependsOnRelocked = false;
                for (int p = 0; p < prereqs.Length; p++)
                {
                    if (prereqs[p].NodeId == node.NodeId &&
                        prereqs[p].PrerequisiteNodeId == relockedNodeId)
                    {
                        dependsOnRelocked = true;
                        break;
                    }
                }

                if (!dependsOnRelocked)
                    continue;

                changes.Add(new ProgressionChangeRecord
                {
                    NodeId = node.NodeId,
                    OldStatus = node.Status,
                    NewStatus = ProgressionNodeStatus.Locked
                });

                node.Status = ProgressionNodeStatus.Locked;
                nodes[i] = node;

                // Recursive cascade
                CascadeRelock(ref nodes, in prereqs, ref changes, node.NodeId);
            }
        }

        static float GetEffectiveMinForResource(
            int resourceId,
            in DynamicBuffer<ResourceElement> resources,
            in DynamicBuffer<ResourceMinModifier> minMods)
        {
            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i].ResourceId == resourceId)
                    return ResourceUtility.GetEffectiveMin(resourceId, resources[i].BaseMin, in minMods);
            }
            return 0f;
        }
    }
}
