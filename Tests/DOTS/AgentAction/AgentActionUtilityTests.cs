using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests for AgentActionUtility.ApplyEffect — the central switch
    /// that processes action effects on completion.
    /// Buffer references are obtained fresh in each test to avoid
    /// invalidation from structural changes.
    /// </summary>
    [TestFixture]
    public class AgentActionUtilityTests : DOTSTestFixture
    {
        Entity _entity;

        public override void SetUp()
        {
            base.SetUp();

            _entity = CreateEntity(
                ComponentType.ReadWrite<StateMachineData>(),
                ComponentType.ReadWrite<ResourceElement>(),
                ComponentType.ReadWrite<ResourceChangeRecord>(),
                ComponentType.ReadWrite<ResourceMaxModifier>(),
                ComponentType.ReadWrite<ResourceMinModifier>(),
                ComponentType.ReadWrite<ActionEffectElement>());

            // Populate resources after entity is fully created (no more structural changes)
            var resources = Manager.GetBuffer<ResourceElement>(_entity);
            resources.Add(new ResourceElement { ResourceId = 1, CurrentValue = 50f, BaseMax = 100f });
            resources.Add(new ResourceElement { ResourceId = 2, CurrentValue = 80f, BaseMax = 100f });
        }

        void ApplyEffect(ActionEffectElement effect)
        {
            // Re-get all buffers fresh to avoid stale handles
            var resources = Manager.GetBuffer<ResourceElement>(_entity);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(_entity);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(_entity);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(_entity);
            var sm = Manager.GetComponentData<StateMachineData>(_entity);

            AgentActionUtility.ApplyEffect(in effect, ref resources, ref changes,
                in maxMods, in minMods, ref sm);

            Manager.SetComponentData(_entity, sm);
        }

        float GetResource(int resourceId)
        {
            var resources = Manager.GetBuffer<ResourceElement>(_entity);
            return ResourceUtility.Get(in resources, resourceId);
        }

        [Test]
        public void AddToResource_IncreasesValue()
        {
            ApplyEffect(new ActionEffectElement
            {
                EffectType = ActionEffectType.AddToResource,
                TargetResourceId = 1, Value = 30f
            });

            Assert.AreEqual(80f, GetResource(1), 0.01f);
        }

        [Test]
        public void AddToResource_ClampsAtMax()
        {
            ApplyEffect(new ActionEffectElement
            {
                EffectType = ActionEffectType.AddToResource,
                TargetResourceId = 1, Value = 200f
            });

            Assert.AreEqual(100f, GetResource(1), 0.01f);
        }

        [Test]
        public void AddToResource_NegativeValue_Decreases()
        {
            ApplyEffect(new ActionEffectElement
            {
                EffectType = ActionEffectType.AddToResource,
                TargetResourceId = 1, Value = -20f
            });

            Assert.AreEqual(30f, GetResource(1), 0.01f);
        }

        [Test]
        public void SetResource_SetsExactValue()
        {
            ApplyEffect(new ActionEffectElement
            {
                EffectType = ActionEffectType.SetResource,
                TargetResourceId = 1, Value = 75f
            });

            Assert.AreEqual(75f, GetResource(1), 0.01f);
        }

        [Test]
        public void TriggerState_RequestsTransition()
        {
            ApplyEffect(new ActionEffectElement
            {
                EffectType = ActionEffectType.TriggerState, Value = 5f
            });

            var sm = Manager.GetComponentData<StateMachineData>(_entity);
            Assert.IsTrue(sm.TransitionRequested);
            Assert.AreEqual(5, sm.RequestedState);
        }

        [Test]
        public void ApplyAllEffects_ProcessesMultiple()
        {
            var effects = Manager.GetBuffer<ActionEffectElement>(_entity);
            effects.Add(new ActionEffectElement
            {
                EffectType = ActionEffectType.AddToResource,
                TargetResourceId = 1, Value = 10f
            });
            effects.Add(new ActionEffectElement
            {
                EffectType = ActionEffectType.AddToResource,
                TargetResourceId = 2, Value = -30f
            });
            effects.Add(new ActionEffectElement
            {
                EffectType = ActionEffectType.TriggerState, Value = 3f
            });

            // Re-get all buffers fresh
            effects = Manager.GetBuffer<ActionEffectElement>(_entity);
            var resources = Manager.GetBuffer<ResourceElement>(_entity);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(_entity);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(_entity);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(_entity);
            var sm = Manager.GetComponentData<StateMachineData>(_entity);

            AgentActionUtility.ApplyAllEffects(in effects, ref resources, ref changes,
                in maxMods, in minMods, ref sm);

            Manager.SetComponentData(_entity, sm);

            Assert.AreEqual(60f, GetResource(1), 0.01f); // 50+10
            Assert.AreEqual(50f, GetResource(2), 0.01f); // 80-30
            Assert.IsTrue(sm.TransitionRequested);
            Assert.AreEqual(3, sm.RequestedState);
        }

        [Test]
        public void AddToResource_CreatesChangeRecord()
        {
            ApplyEffect(new ActionEffectElement
            {
                EffectType = ActionEffectType.AddToResource,
                TargetResourceId = 1, Value = 25f
            });

            var changes = Manager.GetBuffer<ResourceChangeRecord>(_entity);
            Assert.AreEqual(1, changes.Length);
            Assert.AreEqual(1, changes[0].ResourceId);
            Assert.AreEqual(50f, changes[0].OldValue, 0.01f);
            Assert.AreEqual(75f, changes[0].NewValue, 0.01f);
        }

        [Test]
        public void TargetingMissingResource_NoEffect()
        {
            ApplyEffect(new ActionEffectElement
            {
                EffectType = ActionEffectType.AddToResource,
                TargetResourceId = 999, Value = 50f
            });

            var changes = Manager.GetBuffer<ResourceChangeRecord>(_entity);
            Assert.AreEqual(0, changes.Length);
            Assert.AreEqual(50f, GetResource(1), 0.01f);
        }
    }
}
