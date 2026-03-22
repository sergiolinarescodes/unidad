using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests that shared context values influence scoring decisions.
    /// When global context changes, agents switch actions.
    /// </summary>
    [TestFixture]
    public class SharedContextScoringTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        const int NeedHunger = 1;
        const int ActionGather = 10;
        const int ActionIdle = 12;
        const int CtxFoodAvailable = 0;

        public override void SetUp()
        {
            base.SetUp();

            var broadcastSys = GetOrCreateSystem<SharedContextBroadcastSystem>();
            var refreshSys = GetOrCreateSystem<SharedContextRefreshSystem>();
            var strategyAssign = GetOrCreateSystem<StrategyAssignmentSystem>();
            var scoring = GetOrCreateSystem<ScoringSystem>();

            _group = CreateSimGroup(broadcastSys, refreshSys, strategyAssign, scoring);
        }

        Entity CreateBroadcastConfig()
        {
            var e = CreateEntity(ComponentType.ReadWrite<SharedContextBroadcastConfig>());
            Manager.SetComponentData(e, new SharedContextBroadcastConfig { MaxKeys = 16 });
            return e;
        }

        Entity CreateGlobalContext(float foodAvailable)
        {
            var e = CreateEntity(ComponentType.ReadWrite<SharedContextData>());
            Manager.SetComponentData(e, new SharedContextData { ScopeId = 0, ArchetypeId = -1 });
            var entries = AddBuffer<SharedContextEntry>(e);
            entries.Add(new SharedContextEntry { Key = CtxFoodAvailable, Value = foodAvailable });
            AddBuffer<ContextAccessRule>(e);
            return e;
        }

        Entity CreateContextAwareStrategy()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<StrategyDefinition>(),
                ComponentType.ReadWrite<StrategyActionPlanEntry>());

            Manager.SetComponentData(e, new StrategyDefinition { StrategyId = 1 });

            var actions = AddBuffer<StrategyActionElement>(e);
            actions.Add(new StrategyActionElement { ActionId = ActionGather, ActionType = 40 });
            actions.Add(new StrategyActionElement { ActionId = ActionIdle, ActionType = 0 });

            var cons = AddBuffer<StrategyConsiderationTemplate>(e);
            // GatherFood: hunger deficit * food availability step
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionGather, InputType = ScoringInputType.NeedLevel,
                InputParam = NeedHunger, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionGather, InputType = ScoringInputType.AgentContext,
                InputParam = CtxFoodAvailable,
                CurveType = ResponseCurveType.Step, CurveA = 0.01f, CurveB = 1f, CurveC = 0f
            });
            // Idle: constant 0.3
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionIdle, InputType = ScoringInputType.Constant,
                InputParam = 30, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });

            AddBuffer<StrategyActionEffectTemplate>(e);
            AddBuffer<StrategyParamElement>(e);
            return e;
        }

        Entity CreateContextAgent()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>(),
                ComponentType.ReadWrite<ScoringResult>(),
                ComponentType.ReadWrite<ActionSelectionChanged>(),
                ComponentType.ReadWrite<StrategyAssignRequest>(),
                ComponentType.ReadWrite<StrategyAssigned>(),
                ComponentType.ReadWrite<ContextRefreshPolicy>(),
                ComponentType.ReadWrite<ContextRefreshRequest>(),
                ComponentType.ReadWrite<ContextRefreshed>());

            Manager.SetComponentData(e, new AgentData { AgentId = 1, ArchetypeId = 1, StrategyId = -1 });
            Manager.SetComponentData(e, LocalTransform.FromPosition(float3.zero));
            Manager.SetComponentData(e, new ScoringResult { BestActionId = -1, PreviousBestActionId = -1 });
            Manager.SetComponentData(e, new ContextRefreshPolicy
                { Mode = ContextRefreshMode.Interval, RefreshInterval = 0f });

            SetEnabled<ActionSelectionChanged>(e, false);
            SetEnabled<StrategyAssigned>(e, false);
            SetEnabled<ContextRefreshRequest>(e, false);
            SetEnabled<ContextRefreshed>(e, false);

            var resources = AddBuffer<ResourceElement>(e);
            resources.Add(new ResourceElement
                { ResourceId = NeedHunger, CurrentValue = 20f, BaseMax = 100f });
            AddBuffer<ResourceChangeRecord>(e);
            AddBuffer<ResourceMaxModifier>(e);
            AddBuffer<ResourceMinModifier>(e);

            var needs = AddBuffer<NeedElement>(e);
            needs.Add(new NeedElement
            {
                ResourceId = NeedHunger, DecayRate = 0f,
                CriticalThreshold = 10f, LowThreshold = 30f, HighThreshold = 70f,
                CurrentUrgency = NeedUrgency.Low
            });
            AddBuffer<NeedDecayModifier>(e);
            AddBuffer<NeedUrgencyChangeRecord>(e);

            AddBuffer<ConsiderationElement>(e);
            AddBuffer<ActionTimestampElement>(e);
            AddBuffer<StrategyParamElement>(e);
            AddBuffer<AgentContextSnapshot>(e);

            Manager.SetComponentData(e, new StrategyAssignRequest { StrategyId = 1 });
            SetEnabled<StrategyAssignRequest>(e, true);

            return e;
        }

        void Tick(int frames, double startTime = 0.0, float dt = 0.1f)
        {
            for (int i = 0; i < frames; i++)
            {
                SetWorldTime(startTime + (i + 1) * (double)dt, dt);
                UpdateGroup(_group);
            }
        }

        [Test]
        public void FoodAvailable_AgentPicksGather()
        {
            CreateBroadcastConfig();
            CreateGlobalContext(100f);
            CreateContextAwareStrategy();
            var agent = CreateContextAgent();

            Tick(5);

            var result = Manager.GetComponentData<ScoringResult>(agent);
            Assert.AreEqual(ActionGather, result.BestActionId,
                $"Food available → GatherFood. Got {result.BestActionId}, score={result.BestScore:F3}");
        }

        [Test]
        public void NoFood_AgentPicksIdle()
        {
            CreateBroadcastConfig();
            CreateGlobalContext(0f);
            CreateContextAwareStrategy();
            var agent = CreateContextAgent();

            Tick(5);

            var result = Manager.GetComponentData<ScoringResult>(agent);
            Assert.AreEqual(ActionIdle, result.BestActionId,
                $"No food → Idle. Got {result.BestActionId}, score={result.BestScore:F3}");
        }

        [Test]
        public void ContextChange_AgentSwitchesBehavior()
        {
            CreateBroadcastConfig();
            var ctx = CreateGlobalContext(100f);
            CreateContextAwareStrategy();
            var agent = CreateContextAgent();

            // Phase 1: food=100 → Gather
            Tick(5);
            Assert.AreEqual(ActionGather, Manager.GetComponentData<ScoringResult>(agent).BestActionId,
                "Phase 1: GatherFood expected");

            // Phase 2: food=0 → Idle
            var entries = Manager.GetBuffer<SharedContextEntry>(ctx);
            SharedContextUtility.Set(ref entries, CtxFoodAvailable, 0f, 0.0);
            SetEnabled<ContextRefreshRequest>(agent, true);

            Tick(5, startTime: 1.0);
            Assert.AreEqual(ActionIdle, Manager.GetComponentData<ScoringResult>(agent).BestActionId,
                "Phase 2: Idle expected (no food)");

            // Phase 3: food=50 → Gather
            entries = Manager.GetBuffer<SharedContextEntry>(ctx);
            SharedContextUtility.Set(ref entries, CtxFoodAvailable, 50f, 0.0);
            SetEnabled<ContextRefreshRequest>(agent, true);

            Tick(5, startTime: 2.0);
            Assert.AreEqual(ActionGather, Manager.GetComponentData<ScoringResult>(agent).BestActionId,
                "Phase 3: GatherFood expected (food restored)");
        }
    }
}
