using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests for POIClaimSystem — sequential claim processing, capacity enforcement,
    /// claim release on action completion.
    /// </summary>
    [TestFixture]
    public class POIClaimSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();
            var handle = GetOrCreateSystem<POIClaimSystem>();
            _group = CreateSimGroup(handle);
        }

        Entity CreatePOI(float3 position, int poiType, int capacity)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<PointOfInterest>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>());

            Manager.SetComponentData(e, new PointOfInterest
            {
                POIType = poiType, Capacity = capacity, CurrentUsers = 0, IsActive = true
            });
            Manager.SetComponentData(e, LocalTransform.FromPosition(position));
            return e;
        }

        Entity CreateClaimingAgent(Entity poiTarget)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<POIClaim>(),
                ComponentType.ReadWrite<POIClaimRejected>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>());

            Manager.SetComponentData(e, new AgentData { AgentId = 1 });
            Manager.SetComponentData(e, new AgentTarget { TargetEntity = poiTarget });
            Manager.SetComponentData(e, new AgentActionState
            {
                Phase = AgentActionPhase.Starting, CurrentActionId = 1
            });
            Manager.SetComponentData(e, new POIClaim());
            Manager.SetComponentData(e, LocalTransform.FromPosition(float3.zero));

            SetEnabled<POIClaimRejected>(e, false);
            SetEnabled<ActionStarted>(e, true); // Agent just started action
            SetEnabled<ActionCompleted>(e, false);
            SetEnabled<ActionInterrupted>(e, false);

            return e;
        }

        [Test]
        public void ClaimAccepted_WhenCapacityAvailable()
        {
            var poi = CreatePOI(float3.zero, 1, capacity: 3);
            var agent = CreateClaimingAgent(poi);

            UpdateGroup(_group);

            var claim = Manager.GetComponentData<POIClaim>(agent);
            Assert.AreEqual(poi, claim.POIEntity);

            var poiData = Manager.GetComponentData<PointOfInterest>(poi);
            Assert.AreEqual(1, poiData.CurrentUsers);
        }

        [Test]
        public void ClaimRejected_WhenAtCapacity()
        {
            var poi = CreatePOI(float3.zero, 1, capacity: 1);
            // Set POI already at capacity
            Manager.SetComponentData(poi, new PointOfInterest
            {
                POIType = 1, Capacity = 1, CurrentUsers = 1, IsActive = true
            });

            var agent = CreateClaimingAgent(poi);

            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<POIClaimRejected>(agent));
            var claim = Manager.GetComponentData<POIClaim>(agent);
            Assert.AreEqual(Entity.Null, claim.POIEntity, "Claim should not be set when rejected");
        }

        [Test]
        public void ClaimReleased_OnActionCompleted()
        {
            var poi = CreatePOI(float3.zero, 1, capacity: 3);

            // Agent already has an active claim
            var agent = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<POIClaim>(),
                ComponentType.ReadWrite<POIClaimRejected>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>());

            Manager.SetComponentData(agent, new AgentData { AgentId = 1 });
            Manager.SetComponentData(agent, new AgentActionState { Phase = AgentActionPhase.None });
            Manager.SetComponentData(agent, new POIClaim { POIEntity = poi, POIType = 1 });
            Manager.SetComponentData(agent, LocalTransform.FromPosition(float3.zero));

            // POI has 1 user (this agent)
            Manager.SetComponentData(poi, new PointOfInterest
            {
                POIType = 1, Capacity = 3, CurrentUsers = 1, IsActive = true
            });

            SetEnabled<POIClaimRejected>(agent, false);
            SetEnabled<ActionStarted>(agent, false);
            SetEnabled<ActionCompleted>(agent, true); // Action just completed
            SetEnabled<ActionInterrupted>(agent, false);

            UpdateGroup(_group);

            var poiData = Manager.GetComponentData<PointOfInterest>(poi);
            Assert.AreEqual(0, poiData.CurrentUsers, "Users should decrease on release");

            var claim = Manager.GetComponentData<POIClaim>(agent);
            Assert.AreEqual(Entity.Null, claim.POIEntity, "Claim should be cleared");
        }

        [Test]
        public void ClaimReleased_OnDespawn()
        {
            var poi = CreatePOI(float3.zero, 1, capacity: 3);

            var agent = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<POIClaim>(),
                ComponentType.ReadWrite<POIClaimRejected>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>());

            Manager.SetComponentData(agent, new AgentData
            {
                AgentId = 1, LifecycleState = AgentLifecycleState.Despawning
            });
            Manager.SetComponentData(agent, new AgentActionState { Phase = AgentActionPhase.None });
            Manager.SetComponentData(agent, new POIClaim { POIEntity = poi, POIType = 1 });
            Manager.SetComponentData(agent, LocalTransform.FromPosition(float3.zero));

            Manager.SetComponentData(poi, new PointOfInterest
            {
                POIType = 1, Capacity = 3, CurrentUsers = 1, IsActive = true
            });

            SetEnabled<POIClaimRejected>(agent, false);
            SetEnabled<ActionStarted>(agent, false);
            SetEnabled<ActionCompleted>(agent, false);
            SetEnabled<ActionInterrupted>(agent, false);

            UpdateGroup(_group);

            var poiData = Manager.GetComponentData<PointOfInterest>(poi);
            Assert.AreEqual(0, poiData.CurrentUsers, "Despawning should release claim");
        }

        [Test]
        public void MultipleAgents_FirstComesFirst()
        {
            var poi = CreatePOI(float3.zero, 1, capacity: 1);
            var agent1 = CreateClaimingAgent(poi);
            var agent2 = CreateClaimingAgent(poi);

            UpdateGroup(_group);

            var poiData = Manager.GetComponentData<PointOfInterest>(poi);
            Assert.AreEqual(1, poiData.CurrentUsers, "Only one should claim");

            // One should be claimed, one rejected
            var claim1 = Manager.GetComponentData<POIClaim>(agent1);
            var claim2 = Manager.GetComponentData<POIClaim>(agent2);
            bool oneClaimedOneRejected =
                (claim1.POIEntity == poi && claim2.POIEntity == Entity.Null) ||
                (claim2.POIEntity == poi && claim1.POIEntity == Entity.Null);
            Assert.IsTrue(oneClaimedOneRejected, "Exactly one agent should claim the POI");
        }
    }
}
