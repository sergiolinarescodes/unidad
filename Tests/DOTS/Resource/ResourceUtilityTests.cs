using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class ResourceUtilityTests : DOTSTestFixture
    {
        Entity CreateResourceEntity(int resourceId, float current, float baseMin, float baseMax)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<ResourceElement>(),
                ComponentType.ReadWrite<ResourceChangeRecord>(),
                ComponentType.ReadWrite<ResourceMaxModifier>(),
                ComponentType.ReadWrite<ResourceMinModifier>());
            var resources = Manager.GetBuffer<ResourceElement>(e);
            resources.Add(new ResourceElement
            {
                ResourceId = resourceId,
                CurrentValue = current,
                InitialValue = current,
                BaseMin = baseMin,
                BaseMax = baseMax
            });
            return e;
        }

        // --- Get ---

        [Test]
        public void Get_ExistingResource_ReturnsCurrentValue()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            Assert.AreEqual(50f, ResourceUtility.Get(in resources, 1));
        }

        [Test]
        public void Get_NonExistentResource_ReturnsZero()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            Assert.AreEqual(0f, ResourceUtility.Get(in resources, 99));
        }

        // --- GetEffectiveMax ---

        [Test]
        public void GetEffectiveMax_NoModifiers_ReturnsBaseMax()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            Assert.AreEqual(100f, ResourceUtility.GetEffectiveMax(1, 100f, in maxMods));
        }

        [Test]
        public void GetEffectiveMax_WithModifier()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            maxMods.Add(new ResourceMaxModifier
            {
                ResourceId = 1,
                Modifier = new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 50f, IsActive = true }
            });
            Assert.AreEqual(150f, ResourceUtility.GetEffectiveMax(1, 100f, in maxMods));
        }

        [Test]
        public void GetEffectiveMax_FiltersByResourceId()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            maxMods.Add(new ResourceMaxModifier
            {
                ResourceId = 2, // different resource
                Modifier = new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 50f, IsActive = true }
            });
            Assert.AreEqual(100f, ResourceUtility.GetEffectiveMax(1, 100f, in maxMods));
        }

        // --- GetEffectiveMin ---

        [Test]
        public void GetEffectiveMin_NoModifiers_ReturnsBaseMin()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);
            Assert.AreEqual(0f, ResourceUtility.GetEffectiveMin(1, 0f, in minMods));
        }

        [Test]
        public void GetEffectiveMin_WithModifier()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);
            minMods.Add(new ResourceMinModifier
            {
                ResourceId = 1,
                Modifier = new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 10f, IsActive = true }
            });
            Assert.AreEqual(10f, ResourceUtility.GetEffectiveMin(1, 0f, in minMods));
        }

        // --- Set ---

        [Test]
        public void Set_ClampsToRange()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            ResourceUtility.Set(ref resources, ref changes, in maxMods, in minMods, 1, 200f);
            Assert.AreEqual(100f, resources[0].CurrentValue);

            ResourceUtility.Set(ref resources, ref changes, in maxMods, in minMods, 1, -50f);
            Assert.AreEqual(0f, resources[0].CurrentValue);
        }

        [Test]
        public void Set_RecordsChange()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            ResourceUtility.Set(ref resources, ref changes, in maxMods, in minMods, 1, 75f);

            Assert.AreEqual(1, changes.Length);
            Assert.AreEqual(1, changes[0].ResourceId);
            Assert.AreEqual(50f, changes[0].OldValue, 0.001f);
            Assert.AreEqual(75f, changes[0].NewValue, 0.001f);
            Assert.AreEqual(100f, changes[0].EffectiveMax, 0.001f);
            Assert.AreEqual(0f, changes[0].EffectiveMin, 0.001f);
        }

        [Test]
        public void Set_NoRecordWhenUnchanged()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            // Set to nearly the same value (within 0.0001 threshold)
            ResourceUtility.Set(ref resources, ref changes, in maxMods, in minMods, 1, 50.00005f);
            Assert.AreEqual(0, changes.Length);
        }

        [Test]
        public void Set_NonExistentResource_NoOp()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            ResourceUtility.Set(ref resources, ref changes, in maxMods, in minMods, 99, 75f);
            Assert.AreEqual(0, changes.Length);
            Assert.AreEqual(50f, resources[0].CurrentValue);
        }

        // --- Add ---

        [Test]
        public void Add_PositiveAmount()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            ResourceUtility.Add(ref resources, ref changes, in maxMods, in minMods, 1, 20f);
            Assert.AreEqual(70f, resources[0].CurrentValue, 0.001f);
        }

        [Test]
        public void Add_ClampedToMax()
        {
            var e = CreateResourceEntity(1, 90f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            ResourceUtility.Add(ref resources, ref changes, in maxMods, in minMods, 1, 50f);
            Assert.AreEqual(100f, resources[0].CurrentValue, 0.001f);
        }

        [Test]
        public void Add_NegativeAmount()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            ResourceUtility.Add(ref resources, ref changes, in maxMods, in minMods, 1, -20f);
            Assert.AreEqual(30f, resources[0].CurrentValue, 0.001f);
        }

        [Test]
        public void Add_NonExistentResource_NoOp()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            ResourceUtility.Add(ref resources, ref changes, in maxMods, in minMods, 99, 20f);
            Assert.AreEqual(0, changes.Length);
        }

        // --- TrySpend ---

        [Test]
        public void TrySpend_SufficientAmount_ReturnsTrue()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            Assert.IsTrue(ResourceUtility.TrySpend(ref resources, ref changes, in maxMods, in minMods, 1, 30f));
            Assert.AreEqual(20f, resources[0].CurrentValue, 0.001f);
        }

        [Test]
        public void TrySpend_InsufficientAmount_ReturnsFalse()
        {
            var e = CreateResourceEntity(1, 10f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            Assert.IsFalse(ResourceUtility.TrySpend(ref resources, ref changes, in maxMods, in minMods, 1, 20f));
            Assert.AreEqual(10f, resources[0].CurrentValue, 0.001f); // unchanged
        }

        [Test]
        public void TrySpend_ExactlyToMin()
        {
            var e = CreateResourceEntity(1, 30f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            Assert.IsTrue(ResourceUtility.TrySpend(ref resources, ref changes, in maxMods, in minMods, 1, 30f));
            Assert.AreEqual(0f, resources[0].CurrentValue, 0.001f);
        }

        [Test]
        public void TrySpend_NonExistentResource_ReturnsFalse()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);

            Assert.IsFalse(ResourceUtility.TrySpend(ref resources, ref changes, in maxMods, in minMods, 99, 10f));
        }

        [Test]
        public void TrySpend_RespectsMinModifier()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);
            minMods.Add(new ResourceMinModifier
            {
                ResourceId = 1,
                Modifier = new ModifierElement { Id = 1, Op = ModifierOp.Add, Value = 20f, IsActive = true }
            });

            // Can spend down to 20 (effective min)
            Assert.IsTrue(ResourceUtility.TrySpend(ref resources, ref changes, in maxMods, in minMods, 1, 30f));
            Assert.AreEqual(20f, resources[0].CurrentValue, 0.001f);
        }
    }
}
