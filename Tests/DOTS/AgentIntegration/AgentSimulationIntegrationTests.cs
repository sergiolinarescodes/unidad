using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// End-to-end integration tests: full agent pipeline
    /// Needs → Scoring → Navigation → Effects.
    /// </summary>
    [TestFixture]
    public class AgentSimulationIntegrationTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        const int NeedHunger = 1;
        const int NeedEnergy = 2;
        const int ActionGather = 10;
        const int ActionRest = 11;
        const int ActionIdle = 12;

        public override void SetUp()
        {
            base.SetUp();

            // Add ALL agent simulation systems in the correct execution order.
            // Even systems we don't directly test must be present to avoid
            // [UpdateAfter] warnings and ensure proper system discovery.
            var agentClear = GetOrCreateSystem<AgentEventClearSystem>();
            var needClear = GetOrCreateSystem<NeedEventClearSystem>();
            var navClear = GetOrCreateSystem<NavEventClearSystem>();
            var actionClear = GetOrCreateSystem<ActionEventClearSystem>();
            var needDecay = GetOrCreateSystem<NeedDecaySystem>();
            var strategyAssign = GetOrCreateSystem<StrategyAssignmentSystem>();
            var scoring = GetOrCreateSystem<ScoringSystem>();
            var actionQueue = GetOrCreateSystem<ActionQueueSystem>();
            var pathRequest = GetOrCreateSystem<PathRequestSystem>();
            var pathFollow = GetOrCreateSystem<PathFollowSystem>();

            _group = CreateSimGroup(
                agentClear, needClear, navClear, actionClear,
                needDecay, strategyAssign, scoring, actionQueue,
                pathRequest, pathFollow);
        }

        Entity CreateStrategy()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<StrategyDefinition>(),
                ComponentType.ReadWrite<StrategyActionPlanEntry>());

            Manager.SetComponentData(e, new StrategyDefinition { StrategyId = 1 });

            var actions = AddBuffer<StrategyActionElement>(e);
            actions.Add(new StrategyActionElement { ActionId = ActionGather, ActionType = 40 });
            actions.Add(new StrategyActionElement { ActionId = ActionRest, ActionType = 41 });
            actions.Add(new StrategyActionElement { ActionId = ActionIdle, ActionType = 0 });

            var cons = AddBuffer<StrategyConsiderationTemplate>(e);
            // GatherFood: pure hunger deficit (0..1)
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionGather, InputType = ScoringInputType.NeedLevel,
                InputParam = NeedHunger, CurveType = ResponseCurveType.Linear,
                CurveA = 1f, CurveB = 0f
            });
            // Rest: pure energy deficit (0..1)
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionRest, InputType = ScoringInputType.NeedLevel,
                InputParam = NeedEnergy, CurveType = ResponseCurveType.Linear,
                CurveA = 1f, CurveB = 0f
            });
            // Idle: constant 0.25 — beats satisfied-state deficit (0.1) but loses to any real need
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionIdle, InputType = ScoringInputType.Constant,
                InputParam = 25, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });

            AddBuffer<StrategyActionEffectTemplate>(e);
            AddBuffer<StrategyParamElement>(e);
            return e;
        }

        Entity CreateNavGraph()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<NavGraphData>(),
                ComponentType.ReadWrite<NavGraphChanged>());
            Manager.SetComponentData(e, new NavGraphData { GraphId = 0, NodeCount = 3 });
            SetEnabled<NavGraphChanged>(e, false);

            var nodes = AddBuffer<NavNodeElement>(e);
            nodes.Add(new NavNodeElement { NodeId = 0, WorldPosition = new float3(0, 0, 0) });
            nodes.Add(new NavNodeElement { NodeId = 1, WorldPosition = new float3(10, 0, 0) });
            nodes.Add(new NavNodeElement { NodeId = 2, WorldPosition = new float3(0, 0, 10) });

            var edges = AddBuffer<NavEdgeElement>(e);
            edges.Add(new NavEdgeElement { FromNodeId = 0, ToNodeId = 1, Cost = 10f });
            edges.Add(new NavEdgeElement { FromNodeId = 1, ToNodeId = 0, Cost = 10f });
            edges.Add(new NavEdgeElement { FromNodeId = 0, ToNodeId = 2, Cost = 10f });
            edges.Add(new NavEdgeElement { FromNodeId = 2, ToNodeId = 0, Cost = 10f });

            AddBuffer<NavGraphChangeRecord>(e);
            return e;
        }

        /// <summary>
        /// Creates a full agent with all components needed by the complete system pipeline.
        /// </summary>
        Entity CreateFullAgent(float hunger, float energy, int strategyId)
        {
            var e = CreateEntity(
                // Core
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<AgentLocomotion>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>(),
                // Lifecycle events
                ComponentType.ReadWrite<AgentSpawned>(),
                ComponentType.ReadWrite<AgentActivated>(),
                ComponentType.ReadWrite<AgentSuspended>(),
                ComponentType.ReadWrite<AgentDespawning>(),
                // Scoring + Strategy
                ComponentType.ReadWrite<ScoringResult>(),
                ComponentType.ReadWrite<ActionSelectionChanged>(),
                ComponentType.ReadWrite<StrategyAssignRequest>(),
                ComponentType.ReadWrite<StrategyAssigned>(),
                // Action
                ComponentType.ReadWrite<ActionQueueConfig>(),
                // Navigation
                ComponentType.ReadWrite<NavAgent>(),
                ComponentType.ReadWrite<PathRequest>(),
                ComponentType.ReadWrite<PathProgress>(),
                ComponentType.ReadWrite<PathFound>(),
                ComponentType.ReadWrite<PathNotFound>(),
                ComponentType.ReadWrite<PathCompleted>(),
                ComponentType.ReadWrite<NavNodeReached>(),
                ComponentType.ReadWrite<PathInvalidated>(),
                // Need events
                ComponentType.ReadWrite<NeedUrgencyChanged>());

            Manager.SetComponentData(e, new AgentData { AgentId = 1, ArchetypeId = 1, StrategyId = -1 });
            Manager.SetComponentData(e, LocalTransform.FromPosition(float3.zero));
            Manager.SetComponentData(e, new AgentLocomotion
                { BaseMoveSpeed = 10f, CurrentMoveSpeed = 10f, StoppingDistance = 0.5f });
            Manager.SetComponentData(e, new ScoringResult { BestActionId = -1, PreviousBestActionId = -1 });
            Manager.SetComponentData(e, ActionQueueConfig.Default);
            Manager.SetComponentData(e, new NavAgent
                { GraphId = 0, CurrentNodeId = 0, Status = NavAgentStatus.Idle });

            // Disable all enableable tags
            SetEnabled<AgentSpawned>(e, false);
            SetEnabled<AgentActivated>(e, false);
            SetEnabled<AgentSuspended>(e, false);
            SetEnabled<AgentDespawning>(e, false);
            SetEnabled<ActionSelectionChanged>(e, false);
            SetEnabled<StrategyAssigned>(e, false);
            SetEnabled<PathRequest>(e, false);
            SetEnabled<PathFound>(e, false);
            SetEnabled<PathNotFound>(e, false);
            SetEnabled<PathCompleted>(e, false);
            SetEnabled<NavNodeReached>(e, false);
            SetEnabled<PathInvalidated>(e, false);
            SetEnabled<NeedUrgencyChanged>(e, false);

            // Resources
            var resources = AddBuffer<ResourceElement>(e);
            resources.Add(new ResourceElement
                { ResourceId = NeedHunger, CurrentValue = hunger, InitialValue = hunger, BaseMax = 100f });
            resources.Add(new ResourceElement
                { ResourceId = NeedEnergy, CurrentValue = energy, InitialValue = energy, BaseMax = 100f });
            AddBuffer<ResourceChangeRecord>(e);
            AddBuffer<ResourceMaxModifier>(e);
            AddBuffer<ResourceMinModifier>(e);

            // Needs
            var needs = AddBuffer<NeedElement>(e);
            needs.Add(new NeedElement
            {
                ResourceId = NeedHunger, DecayRate = 5f,
                CriticalThreshold = 10f, LowThreshold = 30f, HighThreshold = 70f,
                CurrentUrgency = NeedUtility.EvaluateUrgency(hunger, 10f, 30f, 70f)
            });
            needs.Add(new NeedElement
            {
                ResourceId = NeedEnergy, DecayRate = 1f,
                CriticalThreshold = 5f, LowThreshold = 20f, HighThreshold = 60f,
                CurrentUrgency = NeedUtility.EvaluateUrgency(energy, 5f, 20f, 60f)
            });
            AddBuffer<NeedDecayModifier>(e);
            AddBuffer<NeedUrgencyChangeRecord>(e);

            // Scoring buffers
            AddBuffer<ConsiderationElement>(e);
            AddBuffer<ActionTimestampElement>(e);
            AddBuffer<StrategyParamElement>(e);
            AddBuffer<AgentContextSnapshot>(e);

            // Navigation
            AddBuffer<PathNodeElement>(e);

            // Request strategy
            Manager.SetComponentData(e, new StrategyAssignRequest { StrategyId = strategyId });
            SetEnabled<StrategyAssignRequest>(e, true);

            return e;
        }

        void Tick(int frames, float dt = 0.05f)
        {
            for (int i = 0; i < frames; i++)
            {
                SetWorldTime((i + 1) * (double)dt, dt);
                UpdateGroup(_group);
            }
        }

        [Test]
        public void FullPipeline_StrategyAssigns_And_ScoringWorks()
        {
            CreateStrategy();
            CreateNavGraph();
            var agent = CreateFullAgent(30f, 90f, strategyId: 1);

            // Tick enough for assignment + scoring
            Tick(3);

            Assert.AreEqual(1, Manager.GetComponentData<AgentData>(agent).StrategyId,
                "Strategy should be assigned");

            var result = Manager.GetComponentData<ScoringResult>(agent);
            Assert.AreEqual(ActionGather, result.BestActionId,
                $"Hungry agent should pick GatherFood, got {result.BestActionId}, score={result.BestScore:F3}");
        }

        [Test]
        public void FullPipeline_NeedsDecay_DuringSimulation()
        {
            CreateStrategy();
            CreateNavGraph();
            var agent = CreateFullAgent(50f, 90f, strategyId: 1);

            // Tick 40 frames at 0.05s = 2 seconds. Hunger decays 5/s → lose 10
            Tick(40);

            var resources = Manager.GetBuffer<ResourceElement>(agent);
            float hunger = ResourceUtility.Get(in resources, NeedHunger);
            Assert.Less(hunger, 50f, $"Hunger should have decayed, got {hunger:F1}");
            Assert.Greater(hunger, 35f, $"Hunger decayed too much: {hunger:F1}");
        }

        [Test]
        public void FullPipeline_AgentNavigatesToTarget()
        {
            CreateStrategy();
            CreateNavGraph();
            var agent = CreateFullAgent(50f, 90f, strategyId: 1);

            // Manually request path to node 1
            Manager.SetComponentData(agent, new PathRequest
                { TargetNodeId = 1, TargetWorldPosition = new float3(10, 0, 0) });
            SetEnabled<PathRequest>(agent, true);

            // Speed=10, distance=10 → ~1s. Tick 30 frames at 0.05s = 1.5s
            Tick(30);

            var nav = Manager.GetComponentData<NavAgent>(agent);
            Assert.AreEqual(NavAgentStatus.Arrived, nav.Status,
                $"Agent should have arrived, status={nav.Status}");

            var pos = Manager.GetComponentData<LocalTransform>(agent).Position;
            Assert.AreEqual(10f, pos.x, 1.5f, $"Agent x should be ~10, got {pos.x:F2}");
        }

        [Test]
        public void FullPipeline_ScoringChanges_AsNeedsDrain()
        {
            CreateStrategy();
            CreateNavGraph();
            var agent = CreateFullAgent(90f, 90f, strategyId: 1);

            // Initially both satisfied → Idle wins
            Tick(3);
            Assert.AreEqual(ActionIdle, Manager.GetComponentData<ScoringResult>(agent).BestActionId,
                "Both satisfied → Idle");

            // Tick 200 frames at 0.05s = 10 seconds.
            // Hunger: 90 - 5*10 = 40 (deficit ~0.6)
            // Energy: 90 - 1*10 = 80 (deficit ~0.2)
            // GatherFood (0.6) > Idle (0.25) > Rest (0.2)
            Tick(200);

            var resources = Manager.GetBuffer<ResourceElement>(agent);
            float hunger = ResourceUtility.Get(in resources, NeedHunger);

            var result = Manager.GetComponentData<ScoringResult>(agent);
            Assert.AreEqual(ActionGather, result.BestActionId,
                $"Hunger depleted ({hunger:F1}) → GatherFood should win, got {result.BestActionId}");
        }
    }
}
