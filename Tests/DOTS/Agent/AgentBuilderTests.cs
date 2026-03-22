using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests for AgentBuilder fluent API: validates correct archetype construction,
    /// buffer initialization, optional module composition, and lifecycle events.
    /// </summary>
    [TestFixture]
    public class AgentBuilderTests : DOTSTestFixture
    {
        [Test]
        public void MinimalBuild_HasCoreComponents()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 42)
                .Build();

            Assert.IsTrue(Manager.HasComponent<AgentData>(entity));
            Assert.IsTrue(Manager.HasComponent<AgentTarget>(entity));
            Assert.IsTrue(Manager.HasComponent<AgentLocomotion>(entity));
            Assert.IsTrue(Manager.HasComponent<AgentActivity>(entity));
            Assert.IsTrue(Manager.HasComponent<LocalTransform>(entity));
            Assert.IsTrue(Manager.HasComponent<LocalToWorld>(entity));
            Assert.IsTrue(Manager.HasComponent<ScoringResult>(entity));
            Assert.IsTrue(Manager.HasComponent<AgentActionState>(entity));
            Assert.IsTrue(Manager.HasComponent<ActionQueueConfig>(entity));
            Assert.IsTrue(Manager.HasComponent<StateMachineData>(entity));

            var data = Manager.GetComponentData<AgentData>(entity);
            Assert.AreEqual(1, data.ArchetypeId);
            Assert.AreEqual(42, data.AgentId);
            Assert.AreEqual(AgentLifecycleState.Initializing, data.LifecycleState);
        }

        [Test]
        public void MinimalBuild_HasLifecycleEventTags()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .Build();

            Assert.IsTrue(Manager.HasComponent<AgentSpawned>(entity));
            Assert.IsTrue(Manager.HasComponent<AgentActivated>(entity));
            Assert.IsTrue(Manager.HasComponent<AgentSuspended>(entity));
            Assert.IsTrue(Manager.HasComponent<AgentDespawning>(entity));
            Assert.IsTrue(Manager.HasComponent<ActivityChanged>(entity));

            // AgentSpawned should be enabled on Build
            Assert.IsTrue(IsEnabled<AgentSpawned>(entity),
                "AgentSpawned event should fire on Build");
        }

        [Test]
        public void MinimalBuild_HasRequiredBuffers()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .Build();

            Assert.IsTrue(Manager.HasBuffer<ConsiderationElement>(entity));
            Assert.IsTrue(Manager.HasBuffer<ActionTimestampElement>(entity));
            Assert.IsTrue(Manager.HasBuffer<StrategyParamElement>(entity));
            Assert.IsTrue(Manager.HasBuffer<ActionEffectElement>(entity));
            Assert.IsTrue(Manager.HasBuffer<ActionCompletionRecord>(entity));
            Assert.IsTrue(Manager.HasBuffer<ActionQueueEntry>(entity));
            Assert.IsTrue(Manager.HasBuffer<AgentContextSnapshot>(entity));
            Assert.IsTrue(Manager.HasBuffer<ResourceElement>(entity));
            Assert.IsTrue(Manager.HasBuffer<NeedElement>(entity));
        }

        [Test]
        public void MinimalBuild_DefaultActionState()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .Build();

            var actionState = Manager.GetComponentData<AgentActionState>(entity);
            Assert.AreEqual(-1, actionState.CurrentActionId);

            var scoring = Manager.GetComponentData<ScoringResult>(entity);
            Assert.AreEqual(-1, scoring.BestActionId);
            Assert.AreEqual(-1, scoring.PreviousBestActionId);

            var queueConfig = Manager.GetComponentData<ActionQueueConfig>(entity);
            Assert.AreEqual(ActionQueueMode.SingleAction, queueConfig.Mode);
            Assert.IsTrue(queueConfig.AllowRescore);
        }

        [Test]
        public void WithNeed_CreatesResourceAndNeedElements()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .WithNeed(resourceId: 1, initial: 80f, max: 100f, decayRate: 2f,
                    critical: 10f, low: 30f, high: 70f)
                .WithNeed(resourceId: 2, initial: 50f, max: 100f, decayRate: 1.5f,
                    critical: 5f, low: 20f, high: 60f)
                .Build();

            var resources = Manager.GetBuffer<ResourceElement>(entity);
            Assert.AreEqual(2, resources.Length, "Should have 2 resource elements");
            Assert.AreEqual(1, resources[0].ResourceId);
            Assert.AreEqual(80f, resources[0].CurrentValue, 0.01f);
            Assert.AreEqual(100f, resources[0].BaseMax, 0.01f);

            Assert.AreEqual(2, resources[1].ResourceId);
            Assert.AreEqual(50f, resources[1].CurrentValue, 0.01f);

            var needs = Manager.GetBuffer<NeedElement>(entity);
            Assert.AreEqual(2, needs.Length, "Should have 2 need elements");
            Assert.AreEqual(1, needs[0].ResourceId);
            Assert.AreEqual(2f, needs[0].DecayRate, 0.01f);
            Assert.AreEqual(10f, needs[0].CriticalThreshold, 0.01f);
            Assert.AreEqual(30f, needs[0].LowThreshold, 0.01f);
            Assert.AreEqual(70f, needs[0].HighThreshold, 0.01f);
            Assert.AreEqual(NeedUrgency.Satisfied, needs[0].CurrentUrgency,
                "80 is above high=70, so urgency should be Satisfied");

            Assert.AreEqual(2, needs[1].ResourceId);
            Assert.AreEqual(NeedUrgency.Normal, needs[1].CurrentUrgency,
                "50 is between low=20 and high=60, so urgency should be Normal");
        }

        [Test]
        public void WithNavigation_CreatesNavComponents()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .WithNavigation(graphId: 3, moveSpeed: 5f, stoppingDistance: 1f)
                .Build();

            Assert.IsTrue(Manager.HasComponent<NavAgent>(entity));
            Assert.IsTrue(Manager.HasComponent<PathRequest>(entity));
            Assert.IsTrue(Manager.HasComponent<PathProgress>(entity));
            Assert.IsTrue(Manager.HasBuffer<PathNodeElement>(entity));
            Assert.IsTrue(Manager.HasComponent<PathFound>(entity));
            Assert.IsTrue(Manager.HasComponent<PathNotFound>(entity));
            Assert.IsTrue(Manager.HasComponent<PathCompleted>(entity));
            Assert.IsTrue(Manager.HasComponent<PathInvalidated>(entity));

            var nav = Manager.GetComponentData<NavAgent>(entity);
            Assert.AreEqual(3, nav.GraphId);
            Assert.AreEqual(-1, nav.CurrentNodeId);
            Assert.AreEqual(NavAgentStatus.Idle, nav.Status);

            var loco = Manager.GetComponentData<AgentLocomotion>(entity);
            Assert.AreEqual(5f, loco.BaseMoveSpeed, 0.01f);
            Assert.AreEqual(5f, loco.CurrentMoveSpeed, 0.01f);
            Assert.AreEqual(1f, loco.StoppingDistance, 0.01f);
        }

        [Test]
        public void WithoutNavigation_NoNavComponents()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .Build();

            Assert.IsFalse(Manager.HasComponent<NavAgent>(entity));
            Assert.IsFalse(Manager.HasComponent<PathRequest>(entity));
        }

        [Test]
        public void WithAwareness_CreatesAwarenessComponents()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .WithAwareness(range: 25f, maxPOIs: 12, maxAgents: 6)
                .Build();

            Assert.IsTrue(Manager.HasComponent<AwarenessData>(entity));
            Assert.IsTrue(Manager.HasBuffer<KnownPOIElement>(entity));
            Assert.IsTrue(Manager.HasBuffer<KnownAgentElement>(entity));
            Assert.IsTrue(Manager.HasComponent<POIClaim>(entity));

            var awareness = Manager.GetComponentData<AwarenessData>(entity);
            Assert.AreEqual(25f, awareness.AwarenessRange, 0.01f);
            Assert.AreEqual(12, awareness.MaxKnownPOIs);
            Assert.AreEqual(6, awareness.MaxKnownAgents);
        }

        [Test]
        public void WithFeedback_CreatesFeedbackComponents()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .WithFeedback()
                .Build();

            Assert.IsTrue(Manager.HasComponent<AgentFeedback>(entity));
            Assert.IsTrue(Manager.HasBuffer<ActionFeedbackElement>(entity));
            Assert.IsTrue(Manager.HasComponent<FeedbackEvaluated>(entity));
            Assert.IsTrue(Manager.HasComponent<StrategyUnderperforming>(entity));
        }

        [Test]
        public void WithoutFeedback_NoFeedbackComponents()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .Build();

            Assert.IsFalse(Manager.HasComponent<AgentFeedback>(entity));
        }

        [Test]
        public void WithStrategy_TriggersAssignRequest()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .WithStrategy(strategyId: 5)
                .Build();

            Assert.AreEqual(5, Manager.GetComponentData<AgentData>(entity).StrategyId);
            Assert.IsTrue(IsEnabled<StrategyAssignRequest>(entity),
                "StrategyAssignRequest should be enabled on build");
            Assert.AreEqual(5, Manager.GetComponentData<StrategyAssignRequest>(entity).StrategyId);
        }

        [Test]
        public void WithTransform_SetsPositionRotationScale()
        {
            var pos = new float3(10f, 5f, -3f);
            var rot = quaternion.RotateY(math.PI / 4f);

            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .WithTransform(pos, rot, 0.5f)
                .Build();

            var lt = Manager.GetComponentData<LocalTransform>(entity);
            Assert.AreEqual(10f, lt.Position.x, 0.01f);
            Assert.AreEqual(5f, lt.Position.y, 0.01f);
            Assert.AreEqual(-3f, lt.Position.z, 0.01f);
            Assert.AreEqual(0.5f, lt.Scale, 0.01f);
        }

        [Test]
        public void WithContextRefresh_SetsRefreshPolicy()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .WithContextRefresh(ContextRefreshMode.Interval, refreshInterval: 2.5f)
                .Build();

            var policy = Manager.GetComponentData<ContextRefreshPolicy>(entity);
            Assert.AreEqual(ContextRefreshMode.Interval, policy.Mode);
            Assert.AreEqual(2.5f, policy.RefreshInterval, 0.01f);
        }

        [Test]
        public void WithCustomComponent_AddsToEntity()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .With(new PhysicsBody { Mass = 10f })
                .Build();

            Assert.IsTrue(Manager.HasComponent<PhysicsBody>(entity));
            Assert.AreEqual(10f, Manager.GetComponentData<PhysicsBody>(entity).Mass, 0.01f);
        }

        [Test]
        public void AtPosition_SetsPositionWithDefaults()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .AtPosition(new float3(7f, 0f, 3f))
                .Build();

            var lt = Manager.GetComponentData<LocalTransform>(entity);
            Assert.AreEqual(7f, lt.Position.x, 0.01f);
            Assert.AreEqual(3f, lt.Position.z, 0.01f);
        }

        [Test]
        public void NoStrategy_DoesNotEnableAssignRequest()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 1, agentId: 0)
                .Build();

            Assert.AreEqual(-1, Manager.GetComponentData<AgentData>(entity).StrategyId);
            Assert.IsFalse(IsEnabled<StrategyAssignRequest>(entity),
                "No strategy means StrategyAssignRequest should not be enabled");
        }

        [Test]
        public void FullBuilder_AllModules()
        {
            var entity = AgentBuilder.Create(Manager)
                .WithArchetype(archetypeId: 2, agentId: 99)
                .WithStrategy(strategyId: 7)
                .WithTransform(new float3(1, 2, 3), quaternion.identity, 0.8f)
                .WithNeed(1, initial: 90f, max: 100f, decayRate: 1f,
                    critical: 10f, low: 30f, high: 70f)
                .WithNavigation(graphId: 0, moveSpeed: 4f, stoppingDistance: 0.3f)
                .WithAwareness(range: 15f, maxPOIs: 6, maxAgents: 3)
                .WithContextRefresh(ContextRefreshMode.OnScoring)
                .WithFeedback()
                .Build();

            Assert.IsTrue(Manager.Exists(entity));

            // Verify all modules present
            Assert.IsTrue(Manager.HasComponent<NavAgent>(entity));
            Assert.IsTrue(Manager.HasComponent<AwarenessData>(entity));
            Assert.IsTrue(Manager.HasComponent<AgentFeedback>(entity));
            Assert.AreEqual(1, Manager.GetBuffer<ResourceElement>(entity).Length);
            Assert.AreEqual(1, Manager.GetBuffer<NeedElement>(entity).Length);
            Assert.IsTrue(IsEnabled<AgentSpawned>(entity));
            Assert.IsTrue(IsEnabled<StrategyAssignRequest>(entity));
        }
    }
}
