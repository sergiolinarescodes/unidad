using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class ResourceEventSystemTests : DOTSTestFixture
    {
        Entity CreateResourceEntity(int resourceId, float current, float baseMin, float baseMax)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<ResourceElement>(),
                ComponentType.ReadWrite<ResourceChangeRecord>(),
                ComponentType.ReadWrite<ResourceMaxModifier>(),
                ComponentType.ReadWrite<ResourceMinModifier>(),
                ComponentType.ReadWrite<ResourceChanged>(),
                ComponentType.ReadWrite<ResourceDepleted>(),
                ComponentType.ReadWrite<ResourceFilled>());
            var resources = Manager.GetBuffer<ResourceElement>(e);
            resources.Add(new ResourceElement
            {
                ResourceId = resourceId,
                CurrentValue = current,
                InitialValue = current,
                BaseMin = baseMin,
                BaseMax = baseMax
            });
            SetEnabled<ResourceChanged>(e, false);
            SetEnabled<ResourceDepleted>(e, false);
            SetEnabled<ResourceFilled>(e, false);
            return e;
        }

        // --- Clear System ---

        [Test]
        public void Clear_DisablesAllFlags()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            SetEnabled<ResourceChanged>(e, true);
            SetEnabled<ResourceDepleted>(e, true);
            SetEnabled<ResourceFilled>(e, true);

            var handle = GetOrCreateSystem<ResourceEventClearSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsFalse(IsEnabled<ResourceChanged>(e));
            Assert.IsFalse(IsEnabled<ResourceDepleted>(e));
            Assert.IsFalse(IsEnabled<ResourceFilled>(e));
        }

        [Test]
        public void Clear_ClearsChangeRecords()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            changes.Add(new ResourceChangeRecord { ResourceId = 1, OldValue = 50f, NewValue = 30f });

            var handle = GetOrCreateSystem<ResourceEventClearSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.AreEqual(0, Manager.GetBuffer<ResourceChangeRecord>(e).Length);
        }

        // --- Event System ---

        [Test]
        public void Event_NoChanges_NoFlags()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);

            var handle = GetOrCreateSystem<ResourceEventSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsFalse(IsEnabled<ResourceChanged>(e));
            Assert.IsFalse(IsEnabled<ResourceDepleted>(e));
            Assert.IsFalse(IsEnabled<ResourceFilled>(e));
        }

        [Test]
        public void Event_AnyChange_SetsResourceChanged()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            changes.Add(new ResourceChangeRecord
            {
                ResourceId = 1, OldValue = 50f, NewValue = 40f,
                EffectiveMax = 100f, EffectiveMin = 0f
            });

            var handle = GetOrCreateSystem<ResourceEventSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsTrue(IsEnabled<ResourceChanged>(e));
        }

        [Test]
        public void Event_DepletedThresholdCrossing()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            changes.Add(new ResourceChangeRecord
            {
                ResourceId = 1, OldValue = 5f, NewValue = 0f,
                EffectiveMax = 100f, EffectiveMin = 0f
            });

            var handle = GetOrCreateSystem<ResourceEventSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsTrue(IsEnabled<ResourceDepleted>(e));
        }

        [Test]
        public void Event_FilledThresholdCrossing()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            changes.Add(new ResourceChangeRecord
            {
                ResourceId = 1, OldValue = 95f, NewValue = 100f,
                EffectiveMax = 100f, EffectiveMin = 0f
            });

            var handle = GetOrCreateSystem<ResourceEventSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsTrue(IsEnabled<ResourceFilled>(e));
        }

        [Test]
        public void Event_AlreadyAtMin_NotFiredAgain()
        {
            var e = CreateResourceEntity(1, 0f, 0f, 100f);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            // Value stays at min — no downward crossing
            changes.Add(new ResourceChangeRecord
            {
                ResourceId = 1, OldValue = 0f, NewValue = 0f,
                EffectiveMax = 100f, EffectiveMin = 0f
            });

            var handle = GetOrCreateSystem<ResourceEventSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsFalse(IsEnabled<ResourceDepleted>(e));
        }

        [Test]
        public void Event_AlreadyAtMax_FilledNotFiredAgain()
        {
            var e = CreateResourceEntity(1, 100f, 0f, 100f);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            // Value stays at max — no upward crossing
            changes.Add(new ResourceChangeRecord
            {
                ResourceId = 1, OldValue = 100f, NewValue = 100f,
                EffectiveMax = 100f, EffectiveMin = 0f
            });

            var handle = GetOrCreateSystem<ResourceEventSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsFalse(IsEnabled<ResourceFilled>(e));
        }

        [Test]
        public void Event_BothDepletedAndFilled_InSameFrame()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            // Two changes in same frame: one depletes, one fills
            changes.Add(new ResourceChangeRecord
            {
                ResourceId = 1, OldValue = 5f, NewValue = 0f,
                EffectiveMax = 100f, EffectiveMin = 0f
            });
            changes.Add(new ResourceChangeRecord
            {
                ResourceId = 1, OldValue = 95f, NewValue = 100f,
                EffectiveMax = 100f, EffectiveMin = 0f
            });

            var handle = GetOrCreateSystem<ResourceEventSystem>();
            var group = CreateSimGroup(handle);
            UpdateGroup(group);

            Assert.IsTrue(IsEnabled<ResourceDepleted>(e));
            Assert.IsTrue(IsEnabled<ResourceFilled>(e));
        }

        // --- Integration ---

        [Test]
        public void Integration_ClearMutateEvent()
        {
            var e = CreateResourceEntity(1, 50f, 0f, 100f);

            // Only EventSystem in the group
            var eventHandle = GetOrCreateSystem<ResourceEventSystem>();
            var group = CreateSimGroup(eventHandle);

            // Simulate "clear" phase manually
            SetEnabled<ResourceChanged>(e, false);
            SetEnabled<ResourceDepleted>(e, false);
            SetEnabled<ResourceFilled>(e, false);

            // Mutate via ResourceUtility (adds change records)
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);
            ResourceUtility.Set(ref resources, ref changes, in maxMods, in minMods, 1, 0f);

            // Run EventSystem
            UpdateGroup(group);

            Assert.IsTrue(IsEnabled<ResourceChanged>(e));
            Assert.IsTrue(IsEnabled<ResourceDepleted>(e));
            Assert.IsFalse(IsEnabled<ResourceFilled>(e));
        }
    }
}
