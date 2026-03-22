using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests for Strategy assignment + Scoring system integration.
    /// Verifies scoring picks the correct action based on need levels.
    /// </summary>
    [TestFixture]
    public class ScoringSystemTests : DOTSTestFixture
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
            var strategyAssign = GetOrCreateSystem<StrategyAssignmentSystem>();
            var scoring = GetOrCreateSystem<ScoringSystem>();
            _group = CreateSimGroup(strategyAssign, scoring);
        }

        Entity CreateTestStrategy()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<StrategyDefinition>(),
                ComponentType.ReadWrite<StrategyActionPlanEntry>());

            Manager.SetComponentData(e, new StrategyDefinition { StrategyId = 1 });

            var actions = AddBuffer<StrategyActionElement>(e);
            actions.Add(new StrategyActionElement { ActionId = ActionGather, ActionType = 40 });
            actions.Add(new StrategyActionElement { ActionId = ActionRest, ActionType = 41 });
            actions.Add(new StrategyActionElement { ActionId = ActionIdle, ActionType = 0 });

            // Sorted by ActionId for contiguous-run scoring
            var cons = AddBuffer<StrategyConsiderationTemplate>(e);
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionGather, InputType = ScoringInputType.NeedLevel,
                InputParam = NeedHunger, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionRest, InputType = ScoringInputType.NeedLevel,
                InputParam = NeedEnergy, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionIdle, InputType = ScoringInputType.Constant,
                InputParam = 20, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });

            AddBuffer<StrategyActionEffectTemplate>(e);
            AddBuffer<StrategyParamElement>(e);
            return e;
        }

        /// <summary>
        /// Creates a scoring-ready agent with ALL components needed by both
        /// StrategyAssignmentSystem and ScoringSystem.
        /// </summary>
        Entity CreateAgent(float hunger, float energy, int strategyId)
        {
            var e = CreateEntity(
                // Core identity
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>(),
                // Scoring
                ComponentType.ReadWrite<ScoringResult>(),
                ComponentType.ReadWrite<ActionSelectionChanged>(),
                // Strategy assignment
                ComponentType.ReadWrite<StrategyAssignRequest>(),
                ComponentType.ReadWrite<StrategyAssigned>(),
                // Need urgency event (for NeedDecay, but also required by some queries)
                ComponentType.ReadWrite<NeedUrgencyChanged>());

            Manager.SetComponentData(e, new AgentData { StrategyId = -1 });
            Manager.SetComponentData(e, new ScoringResult { BestActionId = -1, PreviousBestActionId = -1 });
            Manager.SetComponentData(e, LocalTransform.FromPosition(float3.zero));
            SetEnabled<ActionSelectionChanged>(e, false);
            SetEnabled<StrategyAssigned>(e, false);
            SetEnabled<NeedUrgencyChanged>(e, false);

            // Resources
            var resources = AddBuffer<ResourceElement>(e);
            resources.Add(new ResourceElement { ResourceId = NeedHunger, CurrentValue = hunger, BaseMax = 100f });
            resources.Add(new ResourceElement { ResourceId = NeedEnergy, CurrentValue = energy, BaseMax = 100f });
            AddBuffer<ResourceChangeRecord>(e);
            AddBuffer<ResourceMaxModifier>(e);
            AddBuffer<ResourceMinModifier>(e);

            // Needs
            var needs = AddBuffer<NeedElement>(e);
            needs.Add(new NeedElement
            {
                ResourceId = NeedHunger, DecayRate = 0f,
                CriticalThreshold = 10f, LowThreshold = 30f, HighThreshold = 70f,
                CurrentUrgency = NeedUtility.EvaluateUrgency(hunger, 10f, 30f, 70f)
            });
            needs.Add(new NeedElement
            {
                ResourceId = NeedEnergy, DecayRate = 0f,
                CriticalThreshold = 5f, LowThreshold = 20f, HighThreshold = 60f,
                CurrentUrgency = NeedUtility.EvaluateUrgency(energy, 5f, 20f, 60f)
            });
            AddBuffer<NeedDecayModifier>(e);
            AddBuffer<NeedUrgencyChangeRecord>(e);

            // Scoring buffers (populated by StrategyAssignment or manually)
            AddBuffer<ConsiderationElement>(e);
            AddBuffer<ActionTimestampElement>(e);
            AddBuffer<StrategyParamElement>(e);
            AddBuffer<AgentContextSnapshot>(e);

            // Request strategy assignment
            Manager.SetComponentData(e, new StrategyAssignRequest { StrategyId = strategyId });
            SetEnabled<StrategyAssignRequest>(e, true);

            return e;
        }

        void SetResource(Entity e, int resourceId, float value)
        {
            var resources = Manager.GetBuffer<ResourceElement>(e);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(e);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(e);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(e);
            ResourceUtility.Set(ref resources, ref changes, in maxMods, in minMods, resourceId, value);
        }

        [Test]
        public void StrategyAssignment_PopulatesConsiderations()
        {
            CreateTestStrategy();
            var agent = CreateAgent(50f, 50f, strategyId: 1);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var cons = Manager.GetBuffer<ConsiderationElement>(agent);
            Assert.AreEqual(3, cons.Length, "Should have 3 considerations from strategy");

            var timestamps = Manager.GetBuffer<ActionTimestampElement>(agent);
            Assert.AreEqual(3, timestamps.Length, "Should have 3 action timestamps");

            Assert.AreEqual(1, Manager.GetComponentData<AgentData>(agent).StrategyId);
        }

        [Test]
        public void Scoring_PicksGatherFood_WhenHungry()
        {
            CreateTestStrategy();
            var agent = CreateAgent(20f, 90f, strategyId: 1);

            // Tick 1: assign strategy. Tick 2: score.
            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);
            SetWorldTime(0.2, 0.1f);
            UpdateGroup(_group);

            var result = Manager.GetComponentData<ScoringResult>(agent);
            Assert.AreEqual(ActionGather, result.BestActionId,
                $"Expected GatherFood, got {result.BestActionId}, score={result.BestScore:F3}");
        }

        [Test]
        public void Scoring_PicksRest_WhenTired()
        {
            CreateTestStrategy();
            var agent = CreateAgent(90f, 10f, strategyId: 1);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);
            SetWorldTime(0.2, 0.1f);
            UpdateGroup(_group);

            var result = Manager.GetComponentData<ScoringResult>(agent);
            Assert.AreEqual(ActionRest, result.BestActionId,
                $"Expected Rest, got {result.BestActionId}, score={result.BestScore:F3}");
        }

        [Test]
        public void Scoring_PicksIdle_WhenAllSatisfied()
        {
            CreateTestStrategy();
            var agent = CreateAgent(90f, 90f, strategyId: 1);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);
            SetWorldTime(0.2, 0.1f);
            UpdateGroup(_group);

            var result = Manager.GetComponentData<ScoringResult>(agent);
            Assert.AreEqual(ActionIdle, result.BestActionId,
                $"Expected Idle, got {result.BestActionId}, score={result.BestScore:F3}");
        }

        [Test]
        public void Scoring_SwitchesAction_WhenNeedsChange()
        {
            CreateTestStrategy();
            var agent = CreateAgent(20f, 90f, strategyId: 1);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);
            SetWorldTime(0.2, 0.1f);
            UpdateGroup(_group);
            Assert.AreEqual(ActionGather, Manager.GetComponentData<ScoringResult>(agent).BestActionId);

            // Swap needs
            SetResource(agent, NeedHunger, 90f);
            SetResource(agent, NeedEnergy, 10f);

            SetWorldTime(0.3, 0.1f);
            UpdateGroup(_group);

            var result = Manager.GetComponentData<ScoringResult>(agent);
            Assert.AreEqual(ActionRest, result.BestActionId,
                $"After swap: expected Rest, got {result.BestActionId}");
            Assert.IsTrue(result.ActionChanged);
        }
    }
}
