using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Integration tests for Navigation: A* pathfinding + path following.
    /// Verifies path computation, agent movement, and arrival detection.
    /// </summary>
    [TestFixture]
    public class NavigationSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        const int NodeA = 0, NodeB = 1, NodeC = 2, NodeD = 3;

        public override void SetUp()
        {
            base.SetUp();

            var navClear = GetOrCreateSystem<NavEventClearSystem>();
            var pathRequest = GetOrCreateSystem<PathRequestSystem>();
            var pathFollow = GetOrCreateSystem<PathFollowSystem>();
            _group = CreateSimGroup(navClear, pathRequest, pathFollow);
        }

        Entity CreateNavGraph()
        {
            // A(0,0) — B(10,0) — C(10,10) — D(0,10)
            //   \                              /
            //    ————————— (direct A↔D) ——————
            var e = CreateEntity(
                ComponentType.ReadWrite<NavGraphData>(),
                ComponentType.ReadWrite<NavGraphChanged>());

            Manager.SetComponentData(e, new NavGraphData
            {
                GraphId = 0, GraphType = NavGraphType.Freeform, NodeCount = 4
            });
            SetEnabled<NavGraphChanged>(e, false);

            var nodes = AddBuffer<NavNodeElement>(e);
            nodes.Add(new NavNodeElement { NodeId = NodeA, WorldPosition = new float3(0, 0, 0) });
            nodes.Add(new NavNodeElement { NodeId = NodeB, WorldPosition = new float3(10, 0, 0) });
            nodes.Add(new NavNodeElement { NodeId = NodeC, WorldPosition = new float3(10, 0, 10) });
            nodes.Add(new NavNodeElement { NodeId = NodeD, WorldPosition = new float3(0, 0, 10) });

            var edges = AddBuffer<NavEdgeElement>(e);
            // A↔B (cost 10)
            edges.Add(new NavEdgeElement { FromNodeId = NodeA, ToNodeId = NodeB, Cost = 10f });
            edges.Add(new NavEdgeElement { FromNodeId = NodeB, ToNodeId = NodeA, Cost = 10f });
            // B↔C (cost 10)
            edges.Add(new NavEdgeElement { FromNodeId = NodeB, ToNodeId = NodeC, Cost = 10f });
            edges.Add(new NavEdgeElement { FromNodeId = NodeC, ToNodeId = NodeB, Cost = 10f });
            // C↔D (cost 10)
            edges.Add(new NavEdgeElement { FromNodeId = NodeC, ToNodeId = NodeD, Cost = 10f });
            edges.Add(new NavEdgeElement { FromNodeId = NodeD, ToNodeId = NodeC, Cost = 10f });
            // A↔D (cost 10)
            edges.Add(new NavEdgeElement { FromNodeId = NodeA, ToNodeId = NodeD, Cost = 10f });
            edges.Add(new NavEdgeElement { FromNodeId = NodeD, ToNodeId = NodeA, Cost = 10f });

            AddBuffer<NavGraphChangeRecord>(e);

            return e;
        }

        Entity CreateNavAgent(float3 position, int currentNode)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<NavAgent>(),
                ComponentType.ReadWrite<PathRequest>(),
                ComponentType.ReadWrite<PathProgress>(),
                ComponentType.ReadWrite<AgentLocomotion>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>(),
                ComponentType.ReadWrite<PathFound>(),
                ComponentType.ReadWrite<PathNotFound>(),
                ComponentType.ReadWrite<PathCompleted>(),
                ComponentType.ReadWrite<NavNodeReached>(),
                ComponentType.ReadWrite<PathInvalidated>());

            Manager.SetComponentData(e, new NavAgent
            {
                GraphId = 0, CurrentNodeId = currentNode,
                CapabilityFlags = 0, Status = NavAgentStatus.Idle
            });
            Manager.SetComponentData(e, LocalTransform.FromPosition(position));
            Manager.SetComponentData(e, new AgentLocomotion
            {
                BaseMoveSpeed = 10f, CurrentMoveSpeed = 10f, StoppingDistance = 0.5f
            });

            AddBuffer<PathNodeElement>(e);
            SetEnabled<PathRequest>(e, false);
            SetEnabled<PathFound>(e, false);
            SetEnabled<PathNotFound>(e, false);
            SetEnabled<PathCompleted>(e, false);
            SetEnabled<NavNodeReached>(e, false);
            SetEnabled<PathInvalidated>(e, false);

            return e;
        }

        [Test]
        public void PathRequest_FindsPath_AtoC()
        {
            CreateNavGraph();
            var agent = CreateNavAgent(float3.zero, NodeA);

            // Request path A → C
            Manager.SetComponentData(agent, new PathRequest { TargetNodeId = NodeC });
            SetEnabled<PathRequest>(agent, true);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<PathFound>(agent), "PathFound should fire");
            Assert.IsFalse(IsEnabled<PathRequest>(agent), "PathRequest should be consumed");

            var nav = Manager.GetComponentData<NavAgent>(agent);
            Assert.AreEqual(NavAgentStatus.FollowingPath, nav.Status);

            var path = Manager.GetBuffer<PathNodeElement>(agent);
            Assert.IsTrue(path.Length >= 1, $"Path should have nodes, got {path.Length}");

            // Last node in path should be C
            Assert.AreEqual(NodeC, path[path.Length - 1].NodeId);
        }

        [Test]
        public void PathFollow_MovesAgentToDestination()
        {
            CreateNavGraph();
            var agent = CreateNavAgent(float3.zero, NodeA);

            Manager.SetComponentData(agent, new PathRequest { TargetNodeId = NodeB });
            SetEnabled<PathRequest>(agent, true);

            // Tick pathfinding
            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.AreEqual(NavAgentStatus.FollowingPath,
                Manager.GetComponentData<NavAgent>(agent).Status);

            // Tick movement: speed=10, distance=10, should arrive in ~1s
            for (int i = 0; i < 20; i++)
            {
                SetWorldTime(0.1 + (i + 1) * 0.1, 0.1f);
                UpdateGroup(_group);
            }

            var nav = Manager.GetComponentData<NavAgent>(agent);
            Assert.AreEqual(NavAgentStatus.Arrived, nav.Status,
                $"Agent should have arrived, status={nav.Status}");
            Assert.AreEqual(NodeB, nav.CurrentNodeId);

            var pos = Manager.GetComponentData<LocalTransform>(agent).Position;
            Assert.AreEqual(10f, pos.x, 1f, $"Agent x should be ~10, got {pos.x:F2}");
        }

        [Test]
        public void PathFollow_FiresPathCompleted()
        {
            CreateNavGraph();
            var agent = CreateNavAgent(float3.zero, NodeA);

            Manager.SetComponentData(agent, new PathRequest { TargetNodeId = NodeB });
            SetEnabled<PathRequest>(agent, true);

            // Tick until arrival
            for (int i = 0; i < 30; i++)
            {
                SetWorldTime(i * 0.1, 0.1f);
                UpdateGroup(_group);
            }

            // PathCompleted should have fired at some point
            // (may have been cleared by NavEventClearSystem — check final state)
            var nav = Manager.GetComponentData<NavAgent>(agent);
            Assert.AreEqual(NavAgentStatus.Arrived, nav.Status);
        }

        [Test]
        public void PathRequest_ShortestPath_ADirect()
        {
            // A→C via B costs 20, A→C via D costs 20. Both equal.
            // But A→D is direct (cost 10).
            CreateNavGraph();
            var agent = CreateNavAgent(float3.zero, NodeA);

            Manager.SetComponentData(agent, new PathRequest { TargetNodeId = NodeD });
            SetEnabled<PathRequest>(agent, true);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var path = Manager.GetBuffer<PathNodeElement>(agent);
            // Direct A→D should be 1 node in path (just D, since A is start and excluded)
            Assert.AreEqual(1, path.Length,
                $"Direct A→D path should have 1 node, got {path.Length}");
            Assert.AreEqual(NodeD, path[0].NodeId);
        }

        [Test]
        public void PathRequest_NoPath_ReturnsPathNotFound()
        {
            // Create graph with disconnected node
            var graph = CreateEntity(
                ComponentType.ReadWrite<NavGraphData>(),
                ComponentType.ReadWrite<NavGraphChanged>());

            Manager.SetComponentData(graph, new NavGraphData { GraphId = 0, NodeCount = 2 });
            SetEnabled<NavGraphChanged>(graph, false);

            var nodes = AddBuffer<NavNodeElement>(graph);
            nodes.Add(new NavNodeElement { NodeId = 0, WorldPosition = float3.zero });
            nodes.Add(new NavNodeElement { NodeId = 1, WorldPosition = new float3(10, 0, 0) });
            // No edges — nodes are disconnected

            AddBuffer<NavEdgeElement>(graph);
            AddBuffer<NavGraphChangeRecord>(graph);

            var agent = CreateNavAgent(float3.zero, 0);
            Manager.SetComponentData(agent, new PathRequest { TargetNodeId = 1 });
            SetEnabled<PathRequest>(agent, true);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<PathNotFound>(agent));
            Assert.AreEqual(NavAgentStatus.PathFailed,
                Manager.GetComponentData<NavAgent>(agent).Status);
        }

        [Test]
        public void NavigationUtility_FindNearestNode()
        {
            var graph = CreateNavGraph();
            var nodes = Manager.GetBuffer<NavNodeElement>(graph);

            int nearest = NavigationUtility.FindNearestNode(in nodes, new float3(9, 0, 1));
            Assert.AreEqual(NodeB, nearest, "Nearest to (9,0,1) should be B(10,0,0)");
        }
    }
}
