using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests for WorldKnowledgeSystem (spatial awareness) and POIClaimSystem (claim/reject/release).
    /// Verifies KnownPOI/KnownAgent population, range limits, capacity caps, and claim lifecycle.
    /// </summary>
    [TestFixture]
    public class WorldKnowledgeSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _knowledgeGroup;
        SimulationSystemGroup _claimGroup;

        public override void SetUp()
        {
            base.SetUp();

            var knowledge = GetOrCreateSystem<WorldKnowledgeSystem>();
            _knowledgeGroup = CreateSimGroup(knowledge);

            var claim = GetOrCreateSystem<POIClaimSystem>();
            _claimGroup = CreateSimGroup(claim);
        }

        Entity CreateAwareAgent(float3 position, float range, int maxPOIs = 8, int maxAgents = 4)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AwarenessData>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>(),
                ComponentType.ReadWrite<KnowledgeRefreshed>());

            Manager.SetComponentData(e, new AgentData { AgentId = 1, ArchetypeId = 1 });
            Manager.SetComponentData(e, LocalTransform.FromPosition(position));
            Manager.SetComponentData(e, new AwarenessData
            {
                AwarenessRange = range,
                SpatialHashCellSize = 10f,
                MaxKnownPOIs = maxPOIs,
                MaxKnownAgents = maxAgents
            });

            AddBuffer<KnownPOIElement>(e);
            AddBuffer<KnownAgentElement>(e);
            SetEnabled<KnowledgeRefreshed>(e, false);

            return e;
        }

        Entity CreatePOI(float3 position, int poiType, int capacity = 5, int currentUsers = 0)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<PointOfInterest>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>());

            Manager.SetComponentData(e, new PointOfInterest
            {
                POIType = poiType, Capacity = capacity,
                CurrentUsers = currentUsers, IsActive = true
            });
            Manager.SetComponentData(e, LocalTransform.FromPosition(position));

            return e;
        }

        Entity CreateOtherAgent(float3 position, int archetypeId = 2)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>());

            Manager.SetComponentData(e, new AgentData { AgentId = 99, ArchetypeId = archetypeId });
            Manager.SetComponentData(e, LocalTransform.FromPosition(position));

            return e;
        }

        // ---- WorldKnowledgeSystem tests ----

        [Test]
        public void NearbyPOI_AddedToKnownPOIs()
        {
            var agent = CreateAwareAgent(float3.zero, range: 20f);
            CreatePOI(new float3(5, 0, 0), poiType: 1, capacity: 3, currentUsers: 1);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_knowledgeGroup);

            var known = Manager.GetBuffer<KnownPOIElement>(agent);
            Assert.AreEqual(1, known.Length, "Should know 1 nearby POI");
            Assert.AreEqual(1, known[0].POIType);
            Assert.AreEqual(5f, known[0].Distance, 0.5f);
            Assert.AreEqual(1, known[0].CurrentUsers);
            Assert.AreEqual(3, known[0].Capacity);
        }

        [Test]
        public void FarPOI_NotInKnownPOIs()
        {
            var agent = CreateAwareAgent(float3.zero, range: 10f);
            CreatePOI(new float3(50, 0, 0), poiType: 1);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_knowledgeGroup);

            var known = Manager.GetBuffer<KnownPOIElement>(agent);
            Assert.AreEqual(0, known.Length, "Far POI should not be in known list");
        }

        [Test]
        public void InactivePOI_NotDetected()
        {
            var agent = CreateAwareAgent(float3.zero, range: 20f);
            var poi = CreatePOI(new float3(5, 0, 0), poiType: 1);

            // Set inactive
            Manager.SetComponentData(poi, new PointOfInterest
            {
                POIType = 1, Capacity = 5, CurrentUsers = 0, IsActive = false
            });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_knowledgeGroup);

            var known = Manager.GetBuffer<KnownPOIElement>(agent);
            Assert.AreEqual(0, known.Length, "Inactive POI should not be detected");
        }

        [Test]
        public void MaxKnownPOIs_Respected()
        {
            var agent = CreateAwareAgent(float3.zero, range: 50f, maxPOIs: 2);

            CreatePOI(new float3(5, 0, 0), poiType: 1);
            CreatePOI(new float3(8, 0, 0), poiType: 2);
            CreatePOI(new float3(10, 0, 0), poiType: 3);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_knowledgeGroup);

            var known = Manager.GetBuffer<KnownPOIElement>(agent);
            Assert.AreEqual(2, known.Length, "Should cap at MaxKnownPOIs=2");
        }

        [Test]
        public void NearbyAgent_AddedToKnownAgents()
        {
            var agent = CreateAwareAgent(float3.zero, range: 20f);
            CreateOtherAgent(new float3(7, 0, 0), archetypeId: 3);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_knowledgeGroup);

            var known = Manager.GetBuffer<KnownAgentElement>(agent);
            Assert.AreEqual(1, known.Length, "Should know 1 nearby agent");
            Assert.AreEqual(3, known[0].ArchetypeId);
            Assert.AreEqual(7f, known[0].Distance, 0.5f);
        }

        [Test]
        public void Agent_DoesNotDetectItself()
        {
            // Create an agent that is also in the agentQuery (has AgentData + LocalTransform)
            var agent = CreateAwareAgent(float3.zero, range: 20f);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_knowledgeGroup);

            var known = Manager.GetBuffer<KnownAgentElement>(agent);
            Assert.AreEqual(0, known.Length, "Agent should not detect itself");
        }

        [Test]
        public void MaxKnownAgents_Respected()
        {
            var agent = CreateAwareAgent(float3.zero, range: 50f, maxPOIs: 8, maxAgents: 1);

            CreateOtherAgent(new float3(5, 0, 0));
            CreateOtherAgent(new float3(8, 0, 0));

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_knowledgeGroup);

            var known = Manager.GetBuffer<KnownAgentElement>(agent);
            Assert.AreEqual(1, known.Length, "Should cap at MaxKnownAgents=1");
        }

        [Test]
        public void KnowledgeRefreshed_EventFires()
        {
            var agent = CreateAwareAgent(float3.zero, range: 20f);
            CreatePOI(new float3(5, 0, 0), poiType: 1);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_knowledgeGroup);

            Assert.IsTrue(IsEnabled<KnowledgeRefreshed>(agent));
        }

        [Test]
        public void Knowledge_RefreshedEachFrame()
        {
            var agent = CreateAwareAgent(float3.zero, range: 20f);

            // Frame 1: no POIs
            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_knowledgeGroup);
            Assert.AreEqual(0, Manager.GetBuffer<KnownPOIElement>(agent).Length);

            // Add a POI
            CreatePOI(new float3(5, 0, 0), poiType: 1);

            // Frame 2: should detect the new POI
            SetWorldTime(0.2, 0.1f);
            UpdateGroup(_knowledgeGroup);
            Assert.AreEqual(1, Manager.GetBuffer<KnownPOIElement>(agent).Length,
                "Knowledge should refresh to detect new POI");
        }

        // ---- POIClaimSystem tests ----

        Entity CreateClaimAgent(Entity poiEntity)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<POIClaim>(),
                ComponentType.ReadWrite<POIClaimRejected>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>());

            Manager.SetComponentData(e, new AgentData
            {
                AgentId = 1, LifecycleState = AgentLifecycleState.Active
            });
            Manager.SetComponentData(e, new AgentTarget { TargetEntity = poiEntity });
            Manager.SetComponentData(e, new AgentActionState
            {
                CurrentActionId = 10, Phase = AgentActionPhase.Starting
            });
            Manager.SetComponentData(e, new POIClaim { POIEntity = Entity.Null });

            SetEnabled<ActionStarted>(e, true);
            SetEnabled<ActionCompleted>(e, false);
            SetEnabled<ActionInterrupted>(e, false);
            SetEnabled<POIClaimRejected>(e, false);

            return e;
        }

        [Test]
        public void POIClaim_AcceptedWhenCapacityAvailable()
        {
            var poi = CreatePOI(new float3(5, 0, 0), poiType: 1, capacity: 3, currentUsers: 1);
            var agent = CreateClaimAgent(poi);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_claimGroup);

            var claim = Manager.GetComponentData<POIClaim>(agent);
            Assert.AreEqual(poi, claim.POIEntity, "Claim should be accepted");
            Assert.AreEqual(1, claim.POIType);

            var poiData = Manager.GetComponentData<PointOfInterest>(poi);
            Assert.AreEqual(2, poiData.CurrentUsers, "CurrentUsers should increment to 2");
        }

        [Test]
        public void POIClaim_RejectedWhenAtCapacity()
        {
            var poi = CreatePOI(new float3(5, 0, 0), poiType: 1, capacity: 2, currentUsers: 2);
            var agent = CreateClaimAgent(poi);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_claimGroup);

            var claim = Manager.GetComponentData<POIClaim>(agent);
            Assert.AreEqual(Entity.Null, claim.POIEntity, "Claim should be rejected");

            Assert.IsTrue(IsEnabled<POIClaimRejected>(agent), "POIClaimRejected event should fire");

            var poiData = Manager.GetComponentData<PointOfInterest>(poi);
            Assert.AreEqual(2, poiData.CurrentUsers, "CurrentUsers should not change");
        }

        [Test]
        public void POIClaim_ReleasedOnActionCompleted()
        {
            var poi = CreatePOI(new float3(5, 0, 0), poiType: 1, capacity: 3, currentUsers: 2);

            var agent = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<POIClaim>(),
                ComponentType.ReadWrite<POIClaimRejected>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>());

            Manager.SetComponentData(agent, new AgentData
            {
                AgentId = 1, LifecycleState = AgentLifecycleState.Active
            });
            Manager.SetComponentData(agent, new AgentActionState
            {
                CurrentActionId = 10, Phase = AgentActionPhase.Completing
            });
            Manager.SetComponentData(agent, new POIClaim { POIEntity = poi, POIType = 1 });

            SetEnabled<ActionStarted>(agent, false);
            SetEnabled<ActionCompleted>(agent, true); // Action just completed
            SetEnabled<ActionInterrupted>(agent, false);
            SetEnabled<POIClaimRejected>(agent, false);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_claimGroup);

            var claim = Manager.GetComponentData<POIClaim>(agent);
            Assert.AreEqual(Entity.Null, claim.POIEntity, "Claim should be released on completion");

            var poiData = Manager.GetComponentData<PointOfInterest>(poi);
            Assert.AreEqual(1, poiData.CurrentUsers, "CurrentUsers should decrement to 1");
        }

        [Test]
        public void POIClaim_ReleasedOnDespawning()
        {
            var poi = CreatePOI(new float3(5, 0, 0), poiType: 1, capacity: 3, currentUsers: 2);

            var agent = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<POIClaim>(),
                ComponentType.ReadWrite<POIClaimRejected>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>());

            Manager.SetComponentData(agent, new AgentData
            {
                AgentId = 1, LifecycleState = AgentLifecycleState.Despawning
            });
            Manager.SetComponentData(agent, new AgentActionState
            {
                CurrentActionId = 10, Phase = AgentActionPhase.Executing
            });
            Manager.SetComponentData(agent, new POIClaim { POIEntity = poi, POIType = 1 });

            SetEnabled<ActionStarted>(agent, false);
            SetEnabled<ActionCompleted>(agent, false);
            SetEnabled<ActionInterrupted>(agent, false);
            SetEnabled<POIClaimRejected>(agent, false);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_claimGroup);

            var claim = Manager.GetComponentData<POIClaim>(agent);
            Assert.AreEqual(Entity.Null, claim.POIEntity, "Claim should be released on despawn");

            var poiData = Manager.GetComponentData<PointOfInterest>(poi);
            Assert.AreEqual(1, poiData.CurrentUsers);
        }

        [Test]
        public void POIClaim_NullTarget_NoClaim()
        {
            var agent = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<POIClaim>(),
                ComponentType.ReadWrite<POIClaimRejected>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>());

            Manager.SetComponentData(agent, new AgentData { AgentId = 1 });
            Manager.SetComponentData(agent, new AgentTarget { TargetEntity = Entity.Null });
            Manager.SetComponentData(agent, new AgentActionState
            {
                CurrentActionId = 10, Phase = AgentActionPhase.Starting
            });
            Manager.SetComponentData(agent, new POIClaim { POIEntity = Entity.Null });

            SetEnabled<ActionStarted>(agent, true);
            SetEnabled<ActionCompleted>(agent, false);
            SetEnabled<ActionInterrupted>(agent, false);
            SetEnabled<POIClaimRejected>(agent, false);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_claimGroup);

            var claim = Manager.GetComponentData<POIClaim>(agent);
            Assert.AreEqual(Entity.Null, claim.POIEntity, "No claim for null target");
            Assert.IsFalse(IsEnabled<POIClaimRejected>(agent));
        }
    }
}
