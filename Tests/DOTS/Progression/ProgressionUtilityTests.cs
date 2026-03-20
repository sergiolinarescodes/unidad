using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class ProgressionUtilityTests : DOTSTestFixture
    {
        // Standard test tree: A(1)=root → B(2),C(3) → D(4) requires B
        Entity CreateTestTree()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<ProgressionTreeData>());
            Manager.SetComponentData(e, new ProgressionTreeData { TreeId = 1 });

            var nodes = AddBuffer<ProgressionNodeElement>(e,
                new ProgressionNodeElement { NodeId = 1, Status = ProgressionNodeStatus.Available },
                new ProgressionNodeElement { NodeId = 2, Status = ProgressionNodeStatus.Locked },
                new ProgressionNodeElement { NodeId = 3, Status = ProgressionNodeStatus.Locked },
                new ProgressionNodeElement { NodeId = 4, Status = ProgressionNodeStatus.Locked });

            AddBuffer<PrerequisiteElement>(e,
                new PrerequisiteElement { NodeId = 2, PrerequisiteNodeId = 1 },
                new PrerequisiteElement { NodeId = 3, PrerequisiteNodeId = 1 },
                new PrerequisiteElement { NodeId = 4, PrerequisiteNodeId = 2 });

            AddBuffer<NodeCostElement>(e);
            AddBuffer<ProgressionChangeRecord>(e);
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

        // --- FindNode ---

        [Test]
        public void FindNode_ExistingNode_ReturnsIndex()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            Assert.AreEqual(2, ProgressionUtility.FindNode(in nodes, 3)); // C is at index 2
        }

        [Test]
        public void FindNode_NonExistent_ReturnsNegativeOne()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            Assert.AreEqual(-1, ProgressionUtility.FindNode(in nodes, 99));
        }

        // --- ArePrerequisitesMet ---

        [Test]
        public void ArePrerequisitesMet_NoPrereqs_ReturnsTrue()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            Assert.IsTrue(ProgressionUtility.ArePrerequisitesMet(in nodes, in prereqs, 1)); // A has no prereqs
        }

        [Test]
        public void ArePrerequisitesMet_AllMet_ReturnsTrue()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            // Unlock A
            var node = nodes[0];
            node.Status = ProgressionNodeStatus.Unlocked;
            nodes[0] = node;

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            Assert.IsTrue(ProgressionUtility.ArePrerequisitesMet(in nodes, in prereqs, 2)); // B requires A
        }

        [Test]
        public void ArePrerequisitesMet_PartiallyMet_ReturnsFalse()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            // A is Available (not Unlocked), so B's prereq is not met
            Assert.IsFalse(ProgressionUtility.ArePrerequisitesMet(in nodes, in prereqs, 2));
        }

        [Test]
        public void ArePrerequisitesMet_AvailableIsNotUnlocked()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            // A is Available, not Unlocked — B should not pass
            Assert.IsFalse(ProgressionUtility.ArePrerequisitesMet(in nodes, in prereqs, 2));
        }

        [Test]
        public void ArePrerequisitesMet_MissingPrereqNode_ReturnsFalse()
        {
            var e = CreateEntity(ComponentType.ReadWrite<ProgressionTreeData>());
            AddBuffer<ProgressionNodeElement>(e,
                new ProgressionNodeElement { NodeId = 1, Status = ProgressionNodeStatus.Locked });
            AddBuffer<PrerequisiteElement>(e,
                new PrerequisiteElement { NodeId = 1, PrerequisiteNodeId = 99 }); // 99 doesn't exist
            // Re-fetch after structural changes from AddBuffer
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            Assert.IsFalse(ProgressionUtility.ArePrerequisitesMet(in nodes, in prereqs, 1));
        }

        // --- TryUnlock ---

        [Test]
        public void TryUnlock_PrereqsMet_ReturnsTrue()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            // Unlock A first
            var a = nodes[0];
            a.Status = ProgressionNodeStatus.Unlocked;
            nodes[0] = a;

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            Assert.IsTrue(ProgressionUtility.TryUnlock(ref nodes, in prereqs, ref changes, 2));
            Assert.AreEqual(ProgressionNodeStatus.Unlocked, nodes[1].Status);
        }

        [Test]
        public void TryUnlock_PrereqsNotMet_ReturnsFalse()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            Assert.IsFalse(ProgressionUtility.TryUnlock(ref nodes, in prereqs, ref changes, 2));
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[1].Status);
        }

        [Test]
        public void TryUnlock_AlreadyUnlocked_Idempotent()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var a = nodes[0];
            a.Status = ProgressionNodeStatus.Unlocked;
            nodes[0] = a;

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            // Unlock B
            ProgressionUtility.TryUnlock(ref nodes, in prereqs, ref changes, 2);
            int recordCount = changes.Length;
            // Try again
            Assert.IsTrue(ProgressionUtility.TryUnlock(ref nodes, in prereqs, ref changes, 2));
            Assert.AreEqual(recordCount, changes.Length); // no new records
        }

        [Test]
        public void TryUnlock_RecordsChange()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var a = nodes[0];
            a.Status = ProgressionNodeStatus.Unlocked;
            nodes[0] = a;

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            ProgressionUtility.TryUnlock(ref nodes, in prereqs, ref changes, 2);

            // Should have record for B's unlock + records from ScanForNewlyAvailable
            bool found = false;
            for (int i = 0; i < changes.Length; i++)
            {
                if (changes[i].NodeId == 2 && changes[i].NewStatus == ProgressionNodeStatus.Unlocked)
                    found = true;
            }
            Assert.IsTrue(found);
        }

        [Test]
        public void TryUnlock_TriggersAvailabilityScan()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            // Unlock A
            var a = nodes[0];
            a.Status = ProgressionNodeStatus.Unlocked;
            nodes[0] = a;

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            // Unlock A makes B and C available (they depend on A)
            // But we need to call ScanForNewlyAvailable — TryUnlock does this internally
            // Actually, B and C require A to be Unlocked, and A is already Unlocked
            // So after ScanForNewlyAvailable, B and C should become Available
            // But B and C are Locked, and their prereq (A) is Unlocked, so they should become Available
            // Wait — TryUnlock unlocks node 2 (B), then scans. After B is Unlocked, D's prereq is met.
            ProgressionUtility.TryUnlock(ref nodes, in prereqs, ref changes, 2);

            // D requires B. B is now Unlocked. D should become Available.
            Assert.AreEqual(ProgressionNodeStatus.Available, nodes[3].Status); // D
        }

        // --- ForceUnlock ---

        [Test]
        public void ForceUnlock_BypassesPrereqs()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            ProgressionUtility.ForceUnlock(ref nodes, in prereqs, ref changes, 2);
            Assert.AreEqual(ProgressionNodeStatus.Unlocked, nodes[1].Status);
        }

        [Test]
        public void ForceUnlock_AlreadyUnlocked_NoOp()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var a = nodes[0];
            a.Status = ProgressionNodeStatus.Unlocked;
            nodes[0] = a;
            var b = nodes[1];
            b.Status = ProgressionNodeStatus.Unlocked;
            nodes[1] = b;

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            ProgressionUtility.ForceUnlock(ref nodes, in prereqs, ref changes, 2);
            Assert.AreEqual(0, changes.Length); // no change recorded
        }

        [Test]
        public void ForceUnlock_NonExistent_NoOp()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            ProgressionUtility.ForceUnlock(ref nodes, in prereqs, ref changes, 99);
            Assert.AreEqual(0, changes.Length);
        }

        // --- Relock ---

        [Test]
        public void Relock_LocksNode()
        {
            // Use a tree with no prereqs on B so ScanForNewlyAvailable won't re-promote it
            var e = CreateEntity(ComponentType.ReadWrite<ProgressionTreeData>());
            Manager.SetComponentData(e, new ProgressionTreeData { TreeId = 1 });
            var nodes = AddBuffer<ProgressionNodeElement>(e,
                new ProgressionNodeElement { NodeId = 1, Status = ProgressionNodeStatus.Unlocked },
                new ProgressionNodeElement { NodeId = 2, Status = ProgressionNodeStatus.Unlocked });
            // B requires A — after relocking B, A is still Unlocked so B gets re-promoted.
            // Instead give B no prereqs so scan won't promote it (it has none to satisfy).
            // Actually: a node with no prereqs and status Locked will be promoted to Available by scan.
            // So we must make B depend on something that's Locked.
            // Use: B depends on a non-existent node 99 (never met), so scan won't promote.
            AddBuffer<PrerequisiteElement>(e,
                new PrerequisiteElement { NodeId = 2, PrerequisiteNodeId = 99 });
            AddBuffer<NodeCostElement>(e);
            AddBuffer<ProgressionChangeRecord>(e);

            // Re-fetch after structural changes
            nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            ProgressionUtility.Relock(ref nodes, in prereqs, ref changes, 2);
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[1].Status);
        }

        [Test]
        public void Relock_CascadesToDependents()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            // Unlock A, B, C, D
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i]; n.Status = ProgressionNodeStatus.Unlocked; nodes[i] = n;
            }

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            // Relock A — cascades to B,C (depend on A) and D (depends on B)
            // After cascade, ScanForNewlyAvailable runs:
            //   A has no prereqs → promoted back to Available
            //   B,C require A (Available, not Unlocked) → stay Locked
            //   D requires B (Locked) → stays Locked
            ProgressionUtility.Relock(ref nodes, in prereqs, ref changes, 1);

            Assert.AreEqual(ProgressionNodeStatus.Available, nodes[0].Status); // A (root re-promoted)
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[1].Status); // B (cascade)
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[2].Status); // C (cascade)
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[3].Status); // D (cascade from B)
        }

        [Test]
        public void Relock_AlreadyLocked_NoOp()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            ProgressionUtility.Relock(ref nodes, in prereqs, ref changes, 2); // B is already Locked
            Assert.AreEqual(0, changes.Length);
        }

        [Test]
        public void Relock_RecordsAllChanges()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var a = nodes[0]; a.Status = ProgressionNodeStatus.Unlocked; nodes[0] = a;
            var b = nodes[1]; b.Status = ProgressionNodeStatus.Unlocked; nodes[1] = b;

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            ProgressionUtility.Relock(ref nodes, in prereqs, ref changes, 1);

            // Should record: A relocked, B relocked (cascade), C remains Locked (no change)
            // Plus any ScanForNewlyAvailable changes
            Assert.GreaterOrEqual(changes.Length, 2);
        }

        [Test]
        public void Relock_ScansForAvailableAfterCascade()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            // Unlock A and B, C stays Locked
            var a = nodes[0]; a.Status = ProgressionNodeStatus.Unlocked; nodes[0] = a;
            var b = nodes[1]; b.Status = ProgressionNodeStatus.Unlocked; nodes[1] = b;

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            // Relock B — D was Available? No, D was Locked. After relock, scan runs.
            // A is still Unlocked, so B and C should become Available again from scan
            ProgressionUtility.Relock(ref nodes, in prereqs, ref changes, 2);

            // B should be Locked, but then ScanForNewlyAvailable should promote B back to Available
            // since A (its prereq) is still Unlocked
            Assert.AreEqual(ProgressionNodeStatus.Available, nodes[1].Status);
        }

        // --- ResetTree ---

        [Test]
        public void ResetTree_AllToLocked_ThenRootAvailable()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            // Unlock everything
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i]; n.Status = ProgressionNodeStatus.Unlocked; nodes[i] = n;
            }

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            ProgressionUtility.ResetTree(ref nodes, in prereqs, ref changes);

            // A has no prereqs → Available; B,C,D → Locked
            Assert.AreEqual(ProgressionNodeStatus.Available, nodes[0].Status);
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[1].Status);
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[2].Status);
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[3].Status);
        }

        [Test]
        public void ResetTree_RecordsNonLockedChanges()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var a = nodes[0]; a.Status = ProgressionNodeStatus.Unlocked; nodes[0] = a;
            var b = nodes[1]; b.Status = ProgressionNodeStatus.Unlocked; nodes[1] = b;

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            ProgressionUtility.ResetTree(ref nodes, in prereqs, ref changes);

            // Should record A and B being reset + A becoming Available
            Assert.GreaterOrEqual(changes.Length, 2);
        }

        // --- ScanForNewlyAvailable ---

        [Test]
        public void ScanForNewlyAvailable_PromotesEligible()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            // Unlock A
            var a = nodes[0]; a.Status = ProgressionNodeStatus.Unlocked; nodes[0] = a;

            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            ProgressionUtility.ScanForNewlyAvailable(ref nodes, in prereqs, ref changes);

            // B and C require A (Unlocked) → should be Available
            Assert.AreEqual(ProgressionNodeStatus.Available, nodes[1].Status);
            Assert.AreEqual(ProgressionNodeStatus.Available, nodes[2].Status);
            // D requires B (not Unlocked) → stays Locked
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[3].Status);
        }

        [Test]
        public void ScanForNewlyAvailable_SkipsIneligible()
        {
            var e = CreateTestTree();
            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var prereqs = Manager.GetBuffer<PrerequisiteElement>(e);
            var changes = Manager.GetBuffer<ProgressionChangeRecord>(e);

            // A is Available (not Unlocked) — B and C prereqs not met
            ProgressionUtility.ScanForNewlyAvailable(ref nodes, in prereqs, ref changes);

            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[1].Status);
            Assert.AreEqual(ProgressionNodeStatus.Locked, nodes[2].Status);
        }

        // --- CanAfford ---

        [Test]
        public void CanAfford_SufficientResources_ReturnsTrue()
        {
            var e = CreateTestTree();
            AddResourceBuffers(e, 1, 100f, 200f);

            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            // Add cost to node B: 50 of resource 1
            var costs = Manager.GetBuffer<NodeCostElement>(e);
            costs.Add(new NodeCostElement { ResourceId = 1, Amount = 50f });
            var b = nodes[1]; b.CostStartIndex = 0; b.CostCount = 1; nodes[1] = b;

            var resources = Manager.GetBuffer<ResourceElement>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            var nodeB = nodes[1];
            Assert.IsTrue(ProgressionUtility.CanAfford(in nodeB, in costs, in resources, in minMods));
        }

        [Test]
        public void CanAfford_InsufficientResources_ReturnsFalse()
        {
            var e = CreateTestTree();
            AddResourceBuffers(e, 1, 10f, 200f);

            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var costs = Manager.GetBuffer<NodeCostElement>(e);
            costs.Add(new NodeCostElement { ResourceId = 1, Amount = 50f });
            var b = nodes[1]; b.CostStartIndex = 0; b.CostCount = 1; nodes[1] = b;

            var resources = Manager.GetBuffer<ResourceElement>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            var nodeB = nodes[1];
            Assert.IsFalse(ProgressionUtility.CanAfford(in nodeB, in costs, in resources, in minMods));
        }

        [Test]
        public void CanAfford_NoCosts_ReturnsTrue()
        {
            var e = CreateTestTree();
            AddResourceBuffers(e, 1, 100f, 200f);

            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var costs = Manager.GetBuffer<NodeCostElement>(e);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            // Node A has no costs (CostCount=0)
            var nodeA = nodes[0];
            Assert.IsTrue(ProgressionUtility.CanAfford(in nodeA, in costs, in resources, in minMods));
        }

        [Test]
        public void CanAfford_RespectsEffectiveMin()
        {
            var e = CreateTestTree();
            AddResourceBuffers(e, 1, 50f, 200f);

            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var costs = Manager.GetBuffer<NodeCostElement>(e);
            costs.Add(new NodeCostElement { ResourceId = 1, Amount = 40f });
            var b = nodes[1]; b.CostStartIndex = 0; b.CostCount = 1; nodes[1] = b;

            var resources = Manager.GetBuffer<ResourceElement>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);
            minMods.Add(new ResourceMinModifier
            {
                ResourceId = 1,
                Modifier = new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 20f, IsActive = true }
            });

            // Current=50, Min=20, spending 40 would leave 10 < 20
            var nodeB = nodes[1];
            Assert.IsFalse(ProgressionUtility.CanAfford(in nodeB, in costs, in resources, in minMods));
        }

        // --- SpendCosts ---

        [Test]
        public void SpendCosts_DeductsCorrectly()
        {
            var e = CreateTestTree();
            AddResourceBuffers(e, 1, 100f, 200f);

            var nodes = Manager.GetBuffer<ProgressionNodeElement>(e);
            var costs = Manager.GetBuffer<NodeCostElement>(e);
            costs.Add(new NodeCostElement { ResourceId = 1, Amount = 30f });
            var b = nodes[1]; b.CostStartIndex = 0; b.CostCount = 1; nodes[1] = b;

            var resources = Manager.GetBuffer<ResourceElement>(e);
            var resourceChanges = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            var nodeB = nodes[1];
            ProgressionUtility.SpendCosts(in nodeB, in costs,
                ref resources, ref resourceChanges, in maxMods, in minMods);

            Assert.AreEqual(70f, resources[0].CurrentValue, 0.001f);
        }
    }
}
