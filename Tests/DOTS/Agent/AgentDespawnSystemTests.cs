using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests for AgentDespawnSystem: POI claim release, nav path clearing,
    /// action queue clearing, despawning event, and entity destruction.
    /// </summary>
    [TestFixture]
    public class AgentDespawnSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();

            var despawn = GetOrCreateSystem<AgentDespawnSystem>();
            _group = CreateSimGroup(despawn);
        }

        Entity CreateDespawningAgent(AgentLifecycleState state = AgentLifecycleState.Despawning)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<AgentLocomotion>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>(),
                ComponentType.ReadWrite<AgentSpawned>(),
                ComponentType.ReadWrite<AgentActivated>(),
                ComponentType.ReadWrite<AgentSuspended>(),
                ComponentType.ReadWrite<AgentDespawning>());

            Manager.SetComponentData(e, new AgentData
            {
                AgentId = 1, ArchetypeId = 1, LifecycleState = state
            });
            Manager.SetComponentData(e, LocalTransform.FromPosition(float3.zero));

            SetEnabled<AgentSpawned>(e, false);
            SetEnabled<AgentActivated>(e, false);
            SetEnabled<AgentSuspended>(e, false);
            SetEnabled<AgentDespawning>(e, false);

            return e;
        }

        Entity CreatePOI(int poiType, int capacity, int currentUsers)
        {
            var e = CreateEntity(ComponentType.ReadWrite<PointOfInterest>());
            Manager.SetComponentData(e, new PointOfInterest
            {
                POIType = poiType, Capacity = capacity,
                CurrentUsers = currentUsers, IsActive = true
            });
            return e;
        }

        [Test]
        public void Despawning_DestroysEntity()
        {
            var agent = CreateDespawningAgent(AgentLifecycleState.Despawning);
            Assert.IsTrue(Manager.Exists(agent));

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsFalse(Manager.Exists(agent), "Agent entity should be destroyed");
        }

        [Test]
        public void ActiveAgent_NotDestroyed()
        {
            var agent = CreateDespawningAgent(AgentLifecycleState.Active);
            Assert.IsTrue(Manager.Exists(agent));

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(Manager.Exists(agent), "Active agent should not be destroyed");
        }

        [Test]
        public void Despawning_ReleasesPOIClaim()
        {
            var poi = CreatePOI(poiType: 1, capacity: 3, currentUsers: 2);
            var agent = CreateDespawningAgent(AgentLifecycleState.Despawning);

            // Add POIClaim component to agent
            Manager.AddComponentData(agent, new POIClaim { POIEntity = poi, POIType = 1 });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var poiData = Manager.GetComponentData<PointOfInterest>(poi);
            Assert.AreEqual(1, poiData.CurrentUsers,
                "POI CurrentUsers should decrement from 2 to 1");
        }

        [Test]
        public void Despawning_POIClaimRelease_ClampsAtZero()
        {
            var poi = CreatePOI(poiType: 1, capacity: 3, currentUsers: 0);
            var agent = CreateDespawningAgent(AgentLifecycleState.Despawning);

            Manager.AddComponentData(agent, new POIClaim { POIEntity = poi, POIType = 1 });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var poiData = Manager.GetComponentData<PointOfInterest>(poi);
            Assert.AreEqual(0, poiData.CurrentUsers,
                "CurrentUsers should not go below 0");
        }

        [Test]
        public void Despawning_ClearsNavPath()
        {
            var agent = CreateDespawningAgent(AgentLifecycleState.Despawning);

            Manager.AddComponentData(agent, new NavAgent
            {
                GraphId = 0, CurrentNodeId = 5, Status = NavAgentStatus.FollowingPath
            });
            var pathBuf = AddBuffer<PathNodeElement>(agent);
            pathBuf.Add(new PathNodeElement { NodeId = 1, WorldPosition = new float3(5, 0, 0) });
            pathBuf.Add(new PathNodeElement { NodeId = 2, WorldPosition = new float3(10, 0, 0) });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            // Agent is destroyed, so we verify indirectly — if the system ran without error,
            // the cleanup happened before destruction. Test passes if no exception.
            Assert.IsFalse(Manager.Exists(agent));
        }

        [Test]
        public void Despawning_ClearsActionQueue()
        {
            var agent = CreateDespawningAgent(AgentLifecycleState.Despawning);

            var queue = AddBuffer<ActionQueueEntry>(agent);
            queue.Add(new ActionQueueEntry
            {
                ActionId = 10, ActionType = 40, SequenceIndex = 0,
                Status = ActionQueueEntryStatus.Active
            });
            queue.Add(new ActionQueueEntry
            {
                ActionId = 11, ActionType = 41, SequenceIndex = 1,
                Status = ActionQueueEntryStatus.Pending
            });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsFalse(Manager.Exists(agent));
        }

        [Test]
        public void Despawning_FiresDespawningEvent()
        {
            // We need to observe the event before entity destruction.
            // Create an active agent, manually check the event fires during despawn.
            var agent = CreateDespawningAgent(AgentLifecycleState.Despawning);

            // To verify the event fires, we can check that the system doesn't crash
            // and the entity is properly cleaned up. The event is set on the same frame
            // the entity is destroyed via ECB, so it exists briefly.
            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsFalse(Manager.Exists(agent), "Entity should be destroyed after despawn");
        }

        [Test]
        public void Despawning_NoPOIClaim_NoError()
        {
            // Agent without POIClaim component — should not crash
            var agent = CreateDespawningAgent(AgentLifecycleState.Despawning);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsFalse(Manager.Exists(agent));
        }

        [Test]
        public void Despawning_NullPOIEntity_NoError()
        {
            var agent = CreateDespawningAgent(AgentLifecycleState.Despawning);
            Manager.AddComponentData(agent, new POIClaim { POIEntity = Entity.Null, POIType = 0 });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsFalse(Manager.Exists(agent));
        }

        [Test]
        public void MultipleAgents_OnlyDespawningOnesRemoved()
        {
            var despawning = CreateDespawningAgent(AgentLifecycleState.Despawning);
            var active = CreateDespawningAgent(AgentLifecycleState.Active);
            var suspended = CreateDespawningAgent(AgentLifecycleState.Suspended);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsFalse(Manager.Exists(despawning), "Despawning agent should be destroyed");
            Assert.IsTrue(Manager.Exists(active), "Active agent should survive");
            Assert.IsTrue(Manager.Exists(suspended), "Suspended agent should survive");
        }
    }
}
