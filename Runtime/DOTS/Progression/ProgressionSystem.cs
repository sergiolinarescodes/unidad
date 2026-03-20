using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Processes UnlockRequest, RelockRequest, and ResetTreeRequest on progression tree entities.
    /// Enables 1-frame event tags (NodeUnlocked, NodeBecameAvailable, NodeRelocked, TreeReset)
    /// for downstream systems and the bridge.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ProgressionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame's event tags
            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<ProgressionTreeData>>()
                    .WithEntityAccess())
            {
                SystemAPI.SetComponentEnabled<NodeUnlocked>(entity, false);
                SystemAPI.SetComponentEnabled<NodeBecameAvailable>(entity, false);
                SystemAPI.SetComponentEnabled<NodeRelocked>(entity, false);
                SystemAPI.SetComponentEnabled<TreeReset>(entity, false);
            }

            // Process unlock requests
            foreach (var (request, nodes, prereqs, costs, changes, entity) in
                SystemAPI.Query<
                    RefRO<UnlockRequest>,
                    DynamicBuffer<ProgressionNodeElement>,
                    DynamicBuffer<PrerequisiteElement>,
                    DynamicBuffer<NodeCostElement>,
                    DynamicBuffer<ProgressionChangeRecord>>()
                    .WithAll<UnlockRequest>()
                    .WithEntityAccess())
            {
                var nodesBuf = nodes;
                var changesBuf = changes;
                changesBuf.Clear();

                int nodeId = request.ValueRO.NodeId;
                bool force = request.ValueRO.Force;

                if (force)
                {
                    ProgressionUtility.ForceUnlock(ref nodesBuf, in prereqs, ref changesBuf, nodeId);
                }
                else
                {
                    // Check resource costs if entity has resources
                    bool canAfford = true;
                    if (SystemAPI.HasBuffer<ResourceElement>(entity))
                    {
                        int nodeIdx = ProgressionUtility.FindNode(in nodesBuf, nodeId);
                        if (nodeIdx >= 0)
                        {
                            var node = nodesBuf[nodeIdx];
                            if (node.CostCount > 0)
                            {
                                var resources = SystemAPI.GetBuffer<ResourceElement>(entity);
                                var minMods = SystemAPI.GetBuffer<ResourceMinModifier>(entity);
                                canAfford = ProgressionUtility.CanAfford(
                                    in node, in costs, in resources, in minMods);

                                if (canAfford)
                                {
                                    var resourceChanges = SystemAPI.GetBuffer<ResourceChangeRecord>(entity);
                                    var maxMods = SystemAPI.GetBuffer<ResourceMaxModifier>(entity);
                                    ProgressionUtility.SpendCosts(
                                        in node, in costs,
                                        ref resources, ref resourceChanges,
                                        in maxMods, in minMods);
                                }
                            }
                        }
                    }

                    if (canAfford)
                    {
                        ProgressionUtility.TryUnlock(ref nodesBuf, in prereqs, ref changesBuf, nodeId);
                    }
                }

                ApplyEventTags(ref state, entity, in changesBuf);
                SystemAPI.SetComponentEnabled<UnlockRequest>(entity, false);
            }

            // Process relock requests
            foreach (var (request, nodes, prereqs, changes, entity) in
                SystemAPI.Query<
                    RefRO<RelockRequest>,
                    DynamicBuffer<ProgressionNodeElement>,
                    DynamicBuffer<PrerequisiteElement>,
                    DynamicBuffer<ProgressionChangeRecord>>()
                    .WithAll<RelockRequest>()
                    .WithEntityAccess())
            {
                var nodesBuf = nodes;
                var changesBuf = changes;
                changesBuf.Clear();
                ProgressionUtility.Relock(ref nodesBuf, in prereqs, ref changesBuf, request.ValueRO.NodeId);
                ApplyEventTags(ref state, entity, in changesBuf);
                SystemAPI.SetComponentEnabled<RelockRequest>(entity, false);
            }

            // Process reset requests
            foreach (var (nodes, prereqs, changes, entity) in
                SystemAPI.Query<
                    DynamicBuffer<ProgressionNodeElement>,
                    DynamicBuffer<PrerequisiteElement>,
                    DynamicBuffer<ProgressionChangeRecord>>()
                    .WithAll<ResetTreeRequest>()
                    .WithEntityAccess())
            {
                var nodesBuf = nodes;
                var changesBuf = changes;
                changesBuf.Clear();
                ProgressionUtility.ResetTree(ref nodesBuf, in prereqs, ref changesBuf);
                SystemAPI.SetComponentEnabled<TreeReset>(entity, true);

                // Also set individual event tags for any status changes
                ApplyEventTags(ref state, entity, in changesBuf);
                SystemAPI.SetComponentEnabled<ResetTreeRequest>(entity, false);
            }
        }

        static void ApplyEventTags(ref SystemState state, Entity entity,
            in DynamicBuffer<ProgressionChangeRecord> changes)
        {
            for (int i = 0; i < changes.Length; i++)
            {
                var record = changes[i];
                switch (record.NewStatus)
                {
                    case ProgressionNodeStatus.Unlocked:
                        state.EntityManager.SetComponentEnabled<NodeUnlocked>(entity, true);
                        break;
                    case ProgressionNodeStatus.Available:
                        state.EntityManager.SetComponentEnabled<NodeBecameAvailable>(entity, true);
                        break;
                    case ProgressionNodeStatus.Locked:
                        state.EntityManager.SetComponentEnabled<NodeRelocked>(entity, true);
                        break;
                }
            }
        }
    }
}
