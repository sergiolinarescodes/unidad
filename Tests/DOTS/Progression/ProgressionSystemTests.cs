using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class ProgressionSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();
            var handle = GetOrCreateSystem<ProgressionSystem>();
            _group = CreateSimGroup(handle);
        }

        // Standard test tree: A(1)=root → B(2),C(3) → D(4) requires B
        Entity CreateTestTree()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<ProgressionTreeData>(),
                ComponentType.ReadWrite<ProgressionNodeElement>(),
                ComponentType.ReadWrite<PrerequisiteElement>(),
                ComponentType.ReadWrite<NodeCostElement>(),
                ComponentType.ReadWrite<ProgressionChangeRecord>(),
                ComponentType.ReadWrite<UnlockRequest>(),
                ComponentType.ReadWrite<RelockRequest>(),
                ComponentType.ReadWrite<ResetTreeRequest>(),
                ComponentType.ReadWrite<NodeUnlocked>(),
                ComponentType.ReadWrite<NodeBecameAvailable>(),
                ComponentType.ReadWrite<NodeRelocked>(),
                ComponentType.ReadWrite<TreeReset>());

            Manager.SetComponentData(e, new ProgressionTreeData { TreeId = 1 });
            SetEnabled<UnlockRequest>(e, false);
            SetEnabled<RelockRequest>(e, false);
            SetEnabled<ResetTreeRequest>(e, false);
            SetEnabled<NodeUnlocked>(e, false);
            SetEnabled<NodeBecameAvailable>(e, false);
            SetEnabled<NodeRelocked>(e, false);
            SetEnabled<TreeReset>(e, false);

            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            nodes.Add(new ProgressionNodeElement { NodeId = 1, Status = ProgressionNodeStatus.Available });
            nodes.Add(new ProgressionNodeElement { NodeId = 2, Status = ProgressionNodeStatus.Locked });
            nodes.Add(new ProgressionNodeElement { NodeId = 3, Status = ProgressionNodeStatus.Locked });
            nodes.Add(new ProgressionNodeElement { NodeId = 4, Status = ProgressionNodeStatus.Locked });

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            prereqs.Add(new PrerequisiteElement { NodeId = 2, PrerequisiteNodeId = 1 });
            prereqs.Add(new PrerequisiteElement { NodeId = 3, PrerequisiteNodeId = 1 });
            prereqs.Add(new PrerequisiteElement { NodeId = 4, PrerequisiteNodeId = 2 });

            return e;
        }

        void AddResourceBuffers(Entity e, int resourceId, float current, float baseMax)
        {
            AddBuffer<ResourceElement>(e,
                new ResourceElement
                {
                    ResourceId = resourceId,
                    CurrentValue = current,
                    InitialValue = current,
                    BaseMin = 0f,
                    BaseMax = baseMax
                });
            AddBuffer<ResourceChangeRecord>(e);
            AddBuffer<ResourceMaxModifier>(e);
            AddBuffer<ResourceMinModifier>(e);
        }

        // --- UnlockRequest ---

        [Test]
        public void UnlockRequest_UnlocksNode_SetsTag()
        {
            var e = CreateTestTree();
            // A is Available (has no prereqs) — unlock A
            Manager.SetComponentData(e, new UnlockRequest { NodeId = 1, Force = false });
            SetEnabled<UnlockRequest>(e, true);

            UpdateGroup(_group);

            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            Assert.AreEqual(ProgressionNodeStatus.Unlocked, nodes[0].Status);
            Assert.IsTrue(IsEnabled<NodeUnlocked>(e));
        }

        [Test]
        public void UnlockRequest_DependentsBecomeAvailable()
        {
            var e = CreateTestTree();
            // Unlock A → B and C should become Available
            Manager.SetComponentData(e, new UnlockRequest { NodeId = 1, Force = false });
            SetEnabled<UnlockRequest>(e, true);

            UpdateGroup(_group);

            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            Assert.AreEqual(ProgressionNodeStatus.Available, nodes[1].Status); // B
            Assert.AreEqual(ProgressionNodeStatus.Available, nodes[2].Status); // C
            Assert.IsTrue(IsEnabled<NodeBecameAvailable>(e));
        }

        [Test]
        public void UnlockRequest_WithCosts_SpendsResources()
        {
            var e = CreateTestTree();
            AddResourceBuffers(e, 1, 100f, 200f);

            // Add cost to A
            var costs = Manager.GetBuffer<NodeCostElement>(e);
            costs.Add(new NodeCostElement { ResourceId = 1, Amount = 30f });
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var a = nodes[0]; a.CostStartIndex = 0; a.CostCount = 1; nodes[0] = a;

            Manager.SetComponentData(e, new UnlockRequest { NodeId = 1, Force = false });
            SetEnabled<UnlockRequest>(e, true);

            UpdateGroup(_group);

            var resources = Manager.GetBuffer<ResourceElement>(e);
            Assert.AreEqual(70f, resources[0].CurrentValue, 0.001f);
            Assert.AreEqual(ProgressionNodeStatus.Unlocked, nodes[0].Status);
        }

        [Test]
        public void UnlockRequest_CantAfford_NoUnlock()
        {
            var e = CreateTestTree();
            AddResourceBuffers(e, 1, 10f, 200f);

            var costs = Manager.GetBuffer<NodeCostElement>(e);
            costs.Add(new NodeCostElement { ResourceId = 1, Amount = 50f });
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var a = nodes[0]; a.CostStartIndex = 0; a.CostCount = 1; nodes[0] = a;

            Manager.SetComponentData(e, new UnlockRequest { NodeId = 1, Force = false });
            SetEnabled<UnlockRequest>(e, true);

            UpdateGroup(_group);

            nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            Assert.AreEqual(ProgressionNodeStatus.Available, nodes[0].Status); // not unlocked
            Assert.IsFalse(IsEnabled<NodeUnlocked>(e));
        }

        [Test]
        public void UnlockRequest_PrereqsNotMet_NoUnlock()
        {
            var e = CreateTestTree();
            // B requires A which is Available (not Unlocked) — should fail
            Manager.SetComponentData(e, new UnlockRequest { NodeId = 2, Force = false });
            SetEnabled<UnlockRequest>(e, true);

            UpdateGroup(_group);

            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[1].Status);
            Assert.IsFalse(IsEnabled<NodeUnlocked>(e));
        }

        [Test]
        public void UnlockRequest_Force_BypassesPrereqs()
        {
            var e = CreateTestTree();
            // B requires A (not unlocked) — force should bypass
            Manager.SetComponentData(e, new UnlockRequest { NodeId = 2, Force = true });
            SetEnabled<UnlockRequest>(e, true);

            UpdateGroup(_group);

            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            Assert.AreEqual(ProgressionNodeStatus.Unlocked, nodes[1].Status);
        }

        [Test]
        public void UnlockRequest_Force_BypassesCosts()
        {
            var e = CreateTestTree();
            AddResourceBuffers(e, 1, 0f, 200f); // zero resources

            var costs = Manager.GetBuffer<NodeCostElement>(e);
            costs.Add(new NodeCostElement { ResourceId = 1, Amount = 50f });
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var a = nodes[0]; a.CostStartIndex = 0; a.CostCount = 1; nodes[0] = a;

            Manager.SetComponentData(e, new UnlockRequest { NodeId = 1, Force = true });
            SetEnabled<UnlockRequest>(e, true);

            UpdateGroup(_group);

            nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            Assert.AreEqual(ProgressionNodeStatus.Unlocked, nodes[0].Status);
        }

        [Test]
        public void UnlockRequest_DisablesRequestAfterProcessing()
        {
            var e = CreateTestTree();
            Manager.SetComponentData(e, new UnlockRequest { NodeId = 1, Force = false });
            SetEnabled<UnlockRequest>(e, true);

            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<UnlockRequest>(e));
        }

        // --- RelockRequest ---

        [Test]
        public void RelockRequest_LocksWithCascade_SetsTag()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            // Unlock A, B
            var a = nodes[0]; a.Status = ProgressionNodeStatus.Unlocked; nodes[0] = a;
            var b = nodes[1]; b.Status = ProgressionNodeStatus.Unlocked; nodes[1] = b;

            Manager.SetComponentData(e, new RelockRequest { NodeId = 1 });
            SetEnabled<RelockRequest>(e, true);

            UpdateGroup(_group);

            nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[0].Status); // A locked
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[1].Status); // B cascade locked
            Assert.IsTrue(IsEnabled<NodeRelocked>(e));
        }

        [Test]
        public void RelockRequest_DisablesRequestAfterProcessing()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var a = nodes[0]; a.Status = ProgressionNodeStatus.Unlocked; nodes[0] = a;

            Manager.SetComponentData(e, new RelockRequest { NodeId = 1 });
            SetEnabled<RelockRequest>(e, true);

            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<RelockRequest>(e));
        }

        // --- ResetTreeRequest ---

        [Test]
        public void ResetTreeRequest_Resets_SetsTag()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i]; n.Status = ProgressionNodeStatus.Unlocked; nodes[i] = n;
            }

            SetEnabled<ResetTreeRequest>(e, true);

            UpdateGroup(_group);

            nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            Assert.AreEqual(ProgressionNodeStatus.Available, nodes[0].Status); // root
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[1].Status);
            Assert.IsTrue(IsEnabled<TreeReset>(e));
        }

        [Test]
        public void ResetTreeRequest_DisablesRequestAfterProcessing()
        {
            var e = CreateTestTree();
            SetEnabled<ResetTreeRequest>(e, true);

            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<ResetTreeRequest>(e));
        }

        // --- Event Tags ---

        [Test]
        public void EventTags_ClearedNextFrame()
        {
            var e = CreateTestTree();
            Manager.SetComponentData(e, new UnlockRequest { NodeId = 1, Force = false });
            SetEnabled<UnlockRequest>(e, true);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<NodeUnlocked>(e));

            // Next frame: no requests
            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<NodeUnlocked>(e));
            Assert.IsFalse(IsEnabled<NodeBecameAvailable>(e));
            Assert.IsFalse(IsEnabled<NodeRelocked>(e));
            Assert.IsFalse(IsEnabled<TreeReset>(e));
        }

        [Test]
        public void ChangeRecords_ClearedOnNewRequest()
        {
            var e = CreateTestTree();
            Manager.SetComponentData(e, new UnlockRequest { NodeId = 1, Force = false });
            SetEnabled<UnlockRequest>(e, true);
            UpdateGroup(_group);

            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);
            Assert.Greater(changes.Length, 0);

            // Second request: changes should be cleared at start
            Manager.SetComponentData(e, new UnlockRequest { NodeId = 2, Force = false });
            SetEnabled<UnlockRequest>(e, true);
            UpdateGroup(_group);

            changes = Manager.GetBuffer<ProgressionChangeRecord>(e);
            // Records should only reflect the new request, not accumulated
            bool hasOldUnlock = false;
            for (int i = 0; i < changes.Length; i++)
            {
                if (changes[i].NodeId == 1 && changes[i].NewStatus == ProgressionNodeStatus.Unlocked)
                    hasOldUnlock = true;
            }
            Assert.IsFalse(hasOldUnlock);
        }
    }
}
