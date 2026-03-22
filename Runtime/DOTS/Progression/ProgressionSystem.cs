using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Processes UnlockRequest, RelockRequest, and ResetTreeRequest on progression tree entities.
    /// Enables 1-frame event tags (NodeUnlocked, NodeBecameAvailable, NodeRelocked, TreeReset)
    /// for downstream systems and the bridge.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ProgressionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Clear previous frame's event tags
            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<ProgressionTreeData>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<NodeUnlocked>(entity, false);
                ecb.SetComponentEnabled<NodeBecameAvailable>(entity, false);
                ecb.SetComponentEnabled<NodeRelocked>(entity, false);
                ecb.SetComponentEnabled<TreeReset>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Process unlock requests
            var unlockEcb = new EntityCommandBuffer(Allocator.Temp);

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

                ApplyEventTags(ref unlockEcb, entity, in changesBuf);
                unlockEcb.SetComponentEnabled<UnlockRequest>(entity, false);
            }

            unlockEcb.Playback(state.EntityManager);
            unlockEcb.Dispose();

            // Process relock requests
            var relockEcb = new EntityCommandBuffer(Allocator.Temp);

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
                ApplyEventTags(ref relockEcb, entity, in changesBuf);
                relockEcb.SetComponentEnabled<RelockRequest>(entity, false);
            }

            relockEcb.Playback(state.EntityManager);
            relockEcb.Dispose();

            // Process reset requests
            var resetEcb = new EntityCommandBuffer(Allocator.Temp);

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
                resetEcb.SetComponentEnabled<TreeReset>(entity, true);

                ApplyEventTags(ref resetEcb, entity, in changesBuf);
                resetEcb.SetComponentEnabled<ResetTreeRequest>(entity, false);
            }

            resetEcb.Playback(state.EntityManager);
            resetEcb.Dispose();
        }

        static void ApplyEventTags(ref EntityCommandBuffer ecb, Entity entity,
            in DynamicBuffer<ProgressionChangeRecord> changes)
        {
            for (int i = 0; i < changes.Length; i++)
            {
                var record = changes[i];
                switch (record.NewStatus)
                {
                    case ProgressionNodeStatus.Unlocked:
                        ecb.SetComponentEnabled<NodeUnlocked>(entity, true);
                        break;
                    case ProgressionNodeStatus.Available:
                        ecb.SetComponentEnabled<NodeBecameAvailable>(entity, true);
                        break;
                    case ProgressionNodeStatus.Locked:
                        ecb.SetComponentEnabled<NodeRelocked>(entity, true);
                        break;
                }
            }
        }
    }
}
