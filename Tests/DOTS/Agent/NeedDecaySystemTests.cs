using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Integration tests for Agent + Needs + Resource systems working together.
    /// Verifies need decay over time, urgency threshold transitions, and resource clamping.
    /// </summary>
    [TestFixture]
    public class NeedDecaySystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();

            var needClear = GetOrCreateSystem<NeedEventClearSystem>();
            var needDecay = GetOrCreateSystem<NeedDecaySystem>();
            _group = CreateSimGroup(needClear, needDecay);
        }

        Entity CreateAgentWithNeed(float initial, float max, float decayRate,
            float critical, float low, float high)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>(),
                ComponentType.ReadWrite<NeedUrgencyChanged>());

            // Resource buffer (the actual value store)
            var resources = AddBuffer<ResourceElement>(e);
            resources.Add(new ResourceElement
            {
                ResourceId = 1,
                CurrentValue = initial,
                InitialValue = initial,
                BaseMin = 0f,
                BaseMax = max
            });

            AddBuffer<ResourceChangeRecord>(e);
            AddBuffer<ResourceMaxModifier>(e);
            AddBuffer<ResourceMinModifier>(e);

            // Need buffer (decay metadata)
            var needs = AddBuffer<NeedElement>(e);
            needs.Add(new NeedElement
            {
                ResourceId = 1,
                DecayRate = decayRate,
                CriticalThreshold = critical,
                LowThreshold = low,
                HighThreshold = high,
                CurrentUrgency = NeedUtility.EvaluateUrgency(initial, critical, low, high)
            });

            AddBuffer<NeedDecayModifier>(e);
            AddBuffer<NeedUrgencyChangeRecord>(e);
            SetEnabled<NeedUrgencyChanged>(e, false);

            return e;
        }

        float GetResource(Entity e, int resourceId)
        {
            var buf = Manager.GetBuffer<ResourceElement>(e);
            return ResourceUtility.Get(in buf, resourceId);
        }

        NeedUrgency GetUrgency(Entity e, int resourceId)
        {
            var buf = Manager.GetBuffer<NeedElement>(e);
            int idx = NeedUtility.FindNeed(in buf, resourceId);
            return idx >= 0 ? buf[idx].CurrentUrgency : NeedUrgency.Satisfied;
        }

        [Test]
        public void NeedDecays_ReducesResourceOverTime()
        {
            // Hunger: initial=80, decay=10/s
            var e = CreateAgentWithNeed(80f, 100f, 10f, 10f, 30f, 70f);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            // After 1 second at 10/s decay: 80 - 10 = 70
            Assert.AreEqual(70f, GetResource(e, 1), 0.5f);
        }

        [Test]
        public void NeedDecays_ClampsAtZero()
        {
            // Start at 5, decay=20/s — should clamp to 0 after 1s
            var e = CreateAgentWithNeed(5f, 100f, 20f, 10f, 30f, 70f);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            Assert.AreEqual(0f, GetResource(e, 1), 0.01f);
        }

        [Test]
        public void UrgencyTransitions_SatisfiedToNormal()
        {
            // initial=75 (Satisfied, above high=70), decay=10/s
            var e = CreateAgentWithNeed(75f, 100f, 10f, 10f, 30f, 70f);
            Assert.AreEqual(NeedUrgency.Satisfied, GetUrgency(e, 1));

            // After 1s: 75-10=65 → Normal (between low=30 and high=70)
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            Assert.AreEqual(NeedUrgency.Normal, GetUrgency(e, 1));
            Assert.IsTrue(IsEnabled<NeedUrgencyChanged>(e));
        }

        [Test]
        public void UrgencyTransitions_NormalToLow()
        {
            // initial=35 (Normal), decay=10/s
            var e = CreateAgentWithNeed(35f, 100f, 10f, 10f, 30f, 70f);
            Assert.AreEqual(NeedUrgency.Normal, GetUrgency(e, 1));

            // After 1s: 35-10=25 → Low (below low=30, above critical=10)
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            Assert.AreEqual(NeedUrgency.Low, GetUrgency(e, 1));
        }

        [Test]
        public void UrgencyTransitions_LowToCritical()
        {
            // initial=15 (Low), decay=10/s
            var e = CreateAgentWithNeed(15f, 100f, 10f, 10f, 30f, 70f);
            Assert.AreEqual(NeedUrgency.Low, GetUrgency(e, 1));

            // After 1s: 15-10=5 → Critical (below critical=10)
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            Assert.AreEqual(NeedUrgency.Critical, GetUrgency(e, 1));
        }

        [Test]
        public void UrgencyChangeRecord_ContainsTransitionDetails()
        {
            var e = CreateAgentWithNeed(75f, 100f, 10f, 10f, 30f, 70f);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var records = Manager.GetBuffer<NeedUrgencyChangeRecord>(e);
            Assert.AreEqual(1, records.Length);
            Assert.AreEqual(NeedUrgency.Satisfied, records[0].OldUrgency);
            Assert.AreEqual(NeedUrgency.Normal, records[0].NewUrgency);
            Assert.AreEqual(1, records[0].ResourceId);
        }

        [Test]
        public void UrgencyEvents_ClearedNextFrame()
        {
            var e = CreateAgentWithNeed(75f, 100f, 10f, 10f, 30f, 70f);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);
            Assert.IsTrue(IsEnabled<NeedUrgencyChanged>(e));

            // Next frame: urgency doesn't change (65→55, still Normal)
            SetWorldTime(2.0, 1.0f);
            UpdateGroup(_group);
            Assert.IsFalse(IsEnabled<NeedUrgencyChanged>(e));

            var records = Manager.GetBuffer<NeedUrgencyChangeRecord>(e);
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void MultipleNeeds_DecayIndependently()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>(),
                ComponentType.ReadWrite<NeedUrgencyChanged>());

            var resources = AddBuffer<ResourceElement>(e);
            resources.Add(new ResourceElement { ResourceId = 1, CurrentValue = 100f, BaseMax = 100f });
            resources.Add(new ResourceElement { ResourceId = 2, CurrentValue = 100f, BaseMax = 100f });

            AddBuffer<ResourceChangeRecord>(e);
            AddBuffer<ResourceMaxModifier>(e);
            AddBuffer<ResourceMinModifier>(e);

            var needs = AddBuffer<NeedElement>(e);
            needs.Add(new NeedElement
            {
                ResourceId = 1, DecayRate = 20f, // Fast
                CriticalThreshold = 10f, LowThreshold = 30f, HighThreshold = 70f,
                CurrentUrgency = NeedUrgency.Satisfied
            });
            needs.Add(new NeedElement
            {
                ResourceId = 2, DecayRate = 5f, // Slow
                CriticalThreshold = 10f, LowThreshold = 30f, HighThreshold = 70f,
                CurrentUrgency = NeedUrgency.Satisfied
            });

            AddBuffer<NeedDecayModifier>(e);
            AddBuffer<NeedUrgencyChangeRecord>(e);
            SetEnabled<NeedUrgencyChanged>(e, false);

            SetWorldTime(2.0, 2.0f);
            UpdateGroup(_group);

            // Need 1: 100 - 20*2 = 60 (Normal)
            // Need 2: 100 - 5*2 = 90 (Satisfied)
            Assert.AreEqual(60f, GetResource(e, 1), 0.5f);
            Assert.AreEqual(90f, GetResource(e, 2), 0.5f);
            Assert.AreEqual(NeedUrgency.Normal, GetUrgency(e, 1));
            Assert.AreEqual(NeedUrgency.Satisfied, GetUrgency(e, 2));
        }

        [Test]
        public void ZeroDecayRate_NoChange()
        {
            var e = CreateAgentWithNeed(50f, 100f, 0f, 10f, 30f, 70f);

            SetWorldTime(10.0, 10.0f);
            UpdateGroup(_group);

            Assert.AreEqual(50f, GetResource(e, 1), 0.01f);
        }
    }
}
