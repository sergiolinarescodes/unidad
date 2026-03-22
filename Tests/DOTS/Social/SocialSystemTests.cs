using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class SocialSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();
            var eventClear = GetOrCreateSystem<InteractionEventClearSystem>();
            var requestSys = GetOrCreateSystem<InteractionRequestSystem>();
            var execSys = GetOrCreateSystem<InteractionExecutionSystem>();
            _group = CreateSimGroup(eventClear, requestSys, execSys);
        }

        Entity CreateSocialAgent(int agentId)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<InteractionRequest>(),
                ComponentType.ReadWrite<InteractionResponse>(),
                ComponentType.ReadWrite<InteractionState>(),
                ComponentType.ReadWrite<InteractionStarted>(),
                ComponentType.ReadWrite<InteractionCompleted>(),
                ComponentType.ReadWrite<InteractionRejected>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>());

            Manager.SetComponentData(e, new AgentData { AgentId = agentId });
            Manager.SetComponentData(e, new InteractionState { Phase = InteractionPhase.None });
            Manager.SetComponentData(e, LocalTransform.FromPosition(float3.zero));

            SetEnabled<InteractionRequest>(e, false);
            SetEnabled<InteractionResponse>(e, false);
            SetEnabled<InteractionStarted>(e, false);
            SetEnabled<InteractionCompleted>(e, false);
            SetEnabled<InteractionRejected>(e, false);
            SetEnabled<ActionCompleted>(e, false);

            AddBuffer<RelationshipElement>(e);

            return e;
        }

        [Test]
        public void Request_MatchesWithTarget()
        {
            var agent1 = CreateSocialAgent(1);
            var agent2 = CreateSocialAgent(2);

            Manager.SetComponentData(agent1, new InteractionRequest
            {
                TargetAgent = agent2,
                InteractionType = 1
            });
            SetEnabled<InteractionRequest>(agent1, true);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var state1 = Manager.GetComponentData<InteractionState>(agent1);
            var state2 = Manager.GetComponentData<InteractionState>(agent2);

            Assert.AreEqual(InteractionPhase.Active, state1.Phase);
            Assert.AreEqual(agent2, state1.PartnerEntity);
            Assert.AreEqual(InteractionPhase.Active, state2.Phase);
            Assert.AreEqual(agent1, state2.PartnerEntity);
            Assert.IsTrue(IsEnabled<InteractionStarted>(agent1));
        }

        [Test]
        public void Request_RejectsWhenTargetBusy()
        {
            var agent1 = CreateSocialAgent(1);
            var agent2 = CreateSocialAgent(2);

            // Agent2 is already interacting
            Manager.SetComponentData(agent2, new InteractionState
            {
                Phase = InteractionPhase.Active,
                PartnerEntity = Entity.Null
            });

            Manager.SetComponentData(agent1, new InteractionRequest
            {
                TargetAgent = agent2, InteractionType = 1
            });
            SetEnabled<InteractionRequest>(agent1, true);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<InteractionRejected>(agent1));
            Assert.AreEqual(InteractionPhase.None,
                Manager.GetComponentData<InteractionState>(agent1).Phase);
        }

        [Test]
        public void Completion_UpdatesRelationship()
        {
            var agent1 = CreateSocialAgent(1);
            var agent2 = CreateSocialAgent(2);

            // Set up active interaction
            Manager.SetComponentData(agent1, new InteractionState
            {
                Phase = InteractionPhase.Active,
                PartnerEntity = agent2,
                InteractionType = 1
            });

            // Simulate action completed
            SetEnabled<ActionCompleted>(agent1, true);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<InteractionCompleted>(agent1));
            Assert.AreEqual(InteractionPhase.None,
                Manager.GetComponentData<InteractionState>(agent1).Phase);

            // Check relationship updated
            var rels = Manager.GetBuffer<RelationshipElement>(agent1);
            Assert.AreEqual(1, rels.Length);
            Assert.AreEqual(2, rels[0].TargetAgentId);
            Assert.Greater(rels[0].Trust, 0f, "Trust should increase");
        }

        [Test]
        public void Events_ClearedNextFrame()
        {
            var agent = CreateSocialAgent(1);
            SetEnabled<InteractionStarted>(agent, true);
            SetEnabled<InteractionCompleted>(agent, true);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<InteractionStarted>(agent));
            Assert.IsFalse(IsEnabled<InteractionCompleted>(agent));
        }

        [Test]
        public void SocialUtility_ModifyTrust_CreatesNew()
        {
            var e = CreateSocialAgent(1);
            var rels = Manager.GetBuffer<RelationshipElement>(e);

            SocialUtility.ModifyTrust(ref rels, targetAgentId: 5, delta: 0.3f, currentTime: 1.0);

            Assert.AreEqual(1, rels.Length);
            Assert.AreEqual(5, rels[0].TargetAgentId);
            Assert.AreEqual(0.3f, rels[0].Trust, 0.01f);
            Assert.AreEqual(1, rels[0].InteractionCount);
        }

        [Test]
        public void SocialUtility_ModifyTrust_UpdatesExisting()
        {
            var e = CreateSocialAgent(1);
            var rels = Manager.GetBuffer<RelationshipElement>(e);
            rels.Add(new RelationshipElement
            {
                TargetAgentId = 5, Trust = 0.2f, Familiarity = 0.3f, InteractionCount = 2
            });

            SocialUtility.ModifyTrust(ref rels, targetAgentId: 5, delta: 0.5f, currentTime: 2.0);

            Assert.AreEqual(1, rels.Length);
            Assert.AreEqual(0.7f, rels[0].Trust, 0.01f);
            Assert.AreEqual(3, rels[0].InteractionCount);
        }
    }
}
