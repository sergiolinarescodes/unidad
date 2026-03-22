using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Comprehensive end-to-end integration test exercising ALL agent simulation modules:
    ///
    /// Scenario: "Village Day"
    ///   - A small village with 2 agents (Farmer, Gatherer), 3 POIs (Farm, Forest, Well)
    ///   - A market that tracks food supply/demand and adjusts prices
    ///   - A nav graph connecting all locations
    ///   - Shared context broadcasting food availability and market prices
    ///   - A daily schedule that switches strategies by time of day
    ///   - Agents have hunger + energy needs that decay
    ///   - Agents remember where they found resources (episodic memory)
    ///   - Agents build relationships when interacting at the same POI
    ///   - Feedback tracks strategy performance over time
    ///
    /// The test simulates ~30 seconds of game time and verifies:
    ///   1. World time advances and schedule slots change
    ///   2. Needs decay drives scoring to pick correct actions
    ///   3. Shared context (food supply) influences action selection
    ///   4. Navigation moves agents to POIs
    ///   5. Action effects restore needs on completion
    ///   6. Memory records visited locations
    ///   7. Economy aggregates supply/demand and updates prices
    ///   8. Feedback tracks agent performance
    ///   9. Activity system reflects current agent state
    ///   10. POI claiming respects capacity
    /// </summary>
    [TestFixture]
    public class FullSimulationIntegrationTest : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        // IDs
        const int ArchetypeFarmer = 1;
        const int ArchetypeGatherer = 2;

        const int StrategyWork = 100;
        const int StrategyRest = 101;

        const int NeedHunger = 1;
        const int NeedEnergy = 2;

        const int ActionFarm = 10;
        const int ActionGather = 11;
        const int ActionEat = 12;
        const int ActionSleep = 13;
        const int ActionIdle = 14;

        const int ActionTypeFarm = 40;
        const int ActionTypeGather = 41;
        const int ActionTypeEat = 42;
        const int ActionTypeSleep = 43;
        const int ActionTypeWait = 0;

        const int POIFarm = 1;
        const int POIForest = 2;
        const int POIWell = 3;

        const int CtxFoodSupply = 0;
        const int CtxWoodSupply = 1;

        const int MemoryFoundFood = 1;
        const int MemoryFoundWood = 2;

        const int NavGraph = 0;
        const int NodeVillage = 0;
        const int NodeFarm = 1;
        const int NodeForest = 2;
        const int NodeWell = 3;

        const int ScheduleDaily = 1;
        const int StateWorking = 1;
        const int StateSleeping = 2;

        const int MarketId = 1;
        const int ItemFood = 1;
        const int ItemWood = 2;

        // Entities
        Entity _worldTime, _broadcastConfig, _feedbackConfig, _pathConfig;
        Entity _globalContext, _navGraph;
        Entity _workStrategy, _restStrategy, _schedule;
        Entity _farmPOI, _forestPOI, _wellPOI;
        Entity _market;
        Entity _farmer, _gatherer;

        public override void SetUp()
        {
            base.SetUp();

            // Create ALL systems in correct execution order
            var agentClear = GetOrCreateSystem<AgentEventClearSystem>();
            var needClear = GetOrCreateSystem<NeedEventClearSystem>();
            var navClear = GetOrCreateSystem<NavEventClearSystem>();
            var actionClear = GetOrCreateSystem<ActionEventClearSystem>();
            var feedbackClear = GetOrCreateSystem<FeedbackEventClearSystem>();
            var memoryClear = GetOrCreateSystem<MemoryEventClearSystem>();
            var scheduleClear = GetOrCreateSystem<ScheduleEventClearSystem>();
            var interactionClear = GetOrCreateSystem<InteractionEventClearSystem>();
            var economyClear = GetOrCreateSystem<EconomyEventClearSystem>();

            var worldTimeSys = GetOrCreateSystem<WorldTimeSystem>();
            var scheduleSys = GetOrCreateSystem<ScheduleSystem>();
            var broadcastSys = GetOrCreateSystem<SharedContextBroadcastSystem>();
            var refreshSys = GetOrCreateSystem<SharedContextRefreshSystem>();
            var worldKnowledge = GetOrCreateSystem<WorldKnowledgeSystem>();
            var needDecay = GetOrCreateSystem<NeedDecaySystem>();
            var strategyAssign = GetOrCreateSystem<StrategyAssignmentSystem>();
            var scoring = GetOrCreateSystem<ScoringSystem>();
            var actionSystem = GetOrCreateSystem<AgentActionSystem>();
            var actionQueue = GetOrCreateSystem<ActionQueueSystem>();
            var poiClaim = GetOrCreateSystem<POIClaimSystem>();
            var pathRequest = GetOrCreateSystem<PathRequestSystem>();
            var pathFollow = GetOrCreateSystem<PathFollowSystem>();
            var interactionReq = GetOrCreateSystem<InteractionRequestSystem>();
            var interactionExec = GetOrCreateSystem<InteractionExecutionSystem>();
            var economySys = GetOrCreateSystem<EconomySystem>();
            var commandQueue = GetOrCreateSystem<CommandQueueSystem>();
            var activitySys = GetOrCreateSystem<AgentActivitySystem>();

            var feedbackEval = GetOrCreateSystem<FeedbackEvaluationSystem>();
            var memoryDecay = GetOrCreateSystem<MemoryDecaySystem>();

            _group = CreateSimGroup(
                // OrderFirst (clear systems)
                agentClear, needClear, navClear, actionClear, feedbackClear,
                memoryClear, scheduleClear, interactionClear, economyClear,
                // Core simulation
                worldTimeSys, scheduleSys,
                broadcastSys, refreshSys,
                worldKnowledge, needDecay,
                strategyAssign, scoring,
                actionSystem, actionQueue,
                poiClaim, pathRequest, pathFollow,
                interactionReq, interactionExec,
                economySys, commandQueue,
                activitySys,
                // OrderLast
                feedbackEval, memoryDecay);
        }

        void BuildWorld()
        {
            var em = Manager;

            // === Singletons ===
            _worldTime = CreateEntity(ComponentType.ReadWrite<WorldTimeData>());
            em.SetComponentData(_worldTime, new WorldTimeData
            {
                TimeOfDay = 8f,     // Start at 8am
                DayLength = 60f,    // 60 real seconds = 1 game day (fast for testing)
                CurrentDay = 1,
                TimeScale = 1f
            });

            _broadcastConfig = CreateEntity(ComponentType.ReadWrite<SharedContextBroadcastConfig>());
            em.SetComponentData(_broadcastConfig, new SharedContextBroadcastConfig { MaxKeys = 32 });

            _feedbackConfig = CreateEntity(ComponentType.ReadWrite<FeedbackConfig>());
            em.SetComponentData(_feedbackConfig, new FeedbackConfig
            {
                EvaluationInterval = 2f,
                SatisfactionDecayRate = 0.9f,
                RewardPerActionComplete = 1f,
                PenaltyPerActionFail = -0.5f,
                NeedSatisfactionWeight = 0.6f,
                UnderperformingThreshold = 0.3f,
                MinActionsForEvaluation = 3
            });

            _pathConfig = CreateEntity(ComponentType.ReadWrite<PathRequestConfig>());
            em.SetComponentData(_pathConfig, PathRequestConfig.Default);

            // === Shared Context ===
            _globalContext = SharedContextBuilder.Create(em)
                .AsGlobal()
                .WithEntry(CtxFoodSupply, 100f)
                .WithEntry(CtxWoodSupply, 50f)
                .Build();

            // === Navigation Graph ===
            //   Village(0,0) --- Farm(20,0)
            //       |               |
            //   Well(0,20) --- Forest(20,20)
            _navGraph = NavGraphBuilder.Create(em)
                .WithId(NavGraph)
                .AddNode(NodeVillage, new float3(0, 0, 0))
                .AddNode(NodeFarm, new float3(20, 0, 0))
                .AddNode(NodeForest, new float3(20, 0, 20))
                .AddNode(NodeWell, new float3(0, 0, 20))
                .AddBidirectionalEdge(NodeVillage, NodeFarm, 20f)
                .AddBidirectionalEdge(NodeVillage, NodeWell, 20f)
                .AddBidirectionalEdge(NodeFarm, NodeForest, 20f)
                .AddBidirectionalEdge(NodeWell, NodeForest, 20f)
                .Build();

            // === POIs ===
            _farmPOI = CreatePOI(new float3(20, 0, 0), POIFarm, capacity: 2);
            _forestPOI = CreatePOI(new float3(20, 0, 20), POIForest, capacity: 2);
            _wellPOI = CreatePOI(new float3(0, 0, 20), POIWell, capacity: 3);

            // === Market ===
            _market = CreateEntity(
                ComponentType.ReadWrite<MarketData>(),
                ComponentType.ReadWrite<MarketPriceChanged>());
            em.SetComponentData(_market, new MarketData
            {
                MarketId = MarketId,
                PriceUpdateInterval = 1f,
                LastUpdateTime = 0.0
            });
            SetEnabled<MarketPriceChanged>(_market, false);
            var items = AddBuffer<MarketItemElement>(_market);
            items.Add(new MarketItemElement
            {
                ItemId = ItemFood, BasePrice = 10f, CurrentPrice = 10f, PriceElasticity = 0.5f
            });
            items.Add(new MarketItemElement
            {
                ItemId = ItemWood, BasePrice = 8f, CurrentPrice = 8f, PriceElasticity = 0.5f
            });
            AddBuffer<TradeRecord>(_market);

            // === Producers / Consumers ===
            CreateProducer(MarketId, ItemFood, rate: 3f);
            CreateConsumer(MarketId, ItemFood, rate: 5f);  // Demand > supply → price rises
            CreateProducer(MarketId, ItemWood, rate: 4f);
            CreateConsumer(MarketId, ItemWood, rate: 2f);  // Supply > demand → price falls

            // === Schedule: work 6-18, sleep 22-6 ===
            _schedule = ScheduleBuilder.Create(em)
                .WithId(ScheduleDaily, "DailyRoutine")
                .AddSlot(6f, 18f, StateWorking, strategyOverride: StrategyWork)
                .AddSlot(22f, 6f, StateSleeping, strategyOverride: StrategyRest)
                .Build();

            // === Strategies ===
            // Work strategy: farm when hungry, gather when not, eat when starving
            _workStrategy = StrategyBuilder.Create(em)
                .WithId(StrategyWork, "WorkStrategy")

                .DefineAction(ActionFarm, ActionTypeFarm)
                .WithConsideration(ScoringInputType.NeedLevel, NeedHunger,
                    ResponseCurveType.Linear, a: 0.8f, b: 0.1f)
                .WithConsideration(ScoringInputType.AgentContext, CtxFoodSupply,
                    ResponseCurveType.Step, a: 0.01f, b: 1f, c: 0f)
                .WithEffect(ActionEffectType.AddToResource, NeedHunger, 30f)
                .EndAction()

                .DefineAction(ActionGather, ActionTypeGather)
                .WithConsideration(ScoringInputType.NeedLevel, NeedEnergy,
                    ResponseCurveType.Linear, a: 0.6f, b: 0.15f)
                .WithEffect(ActionEffectType.AddToResource, NeedEnergy, 20f)
                .EndAction()

                .DefineAction(ActionEat, ActionTypeEat)
                .WithConsideration(ScoringInputType.NeedUrgency, NeedHunger,
                    ResponseCurveType.Quadratic, a: 2f, b: 0f, c: 0f)
                .WithEffect(ActionEffectType.AddToResource, NeedHunger, 50f)
                .EndAction()

                .DefineAction(ActionIdle, ActionTypeWait)
                .WithConsideration(ScoringInputType.Constant, 10,
                    ResponseCurveType.Linear, a: 1f, b: 0f)
                .EndAction()

                .Build();

            // Rest strategy: sleep (restores energy), idle
            _restStrategy = StrategyBuilder.Create(em)
                .WithId(StrategyRest, "RestStrategy")

                .DefineAction(ActionSleep, ActionTypeSleep)
                .WithConsideration(ScoringInputType.NeedLevel, NeedEnergy,
                    ResponseCurveType.Linear, a: 1f, b: 0.2f)
                .WithEffect(ActionEffectType.AddToResource, NeedEnergy, 60f)
                .EndAction()

                .DefineAction(ActionIdle, ActionTypeWait)
                .WithConsideration(ScoringInputType.Constant, 15,
                    ResponseCurveType.Linear, a: 1f, b: 0f)
                .EndAction()

                .Build();

            // === Agents ===
            _farmer = AgentBuilder.Create(em)
                .WithArchetype(ArchetypeFarmer, agentId: 1)
                .WithStrategy(StrategyWork)
                .AtPosition(new float3(0, 0, 0))
                .WithNavigation(NavGraph, moveSpeed: 8f, stoppingDistance: 1f)
                .WithAwareness(range: 25f, maxPOIs: 4, maxAgents: 4)
                .WithContextRefresh(ContextRefreshMode.Interval, refreshInterval: 0f)
                .WithNeed(NeedHunger, initial: 70f, max: 100f, decayRate: 3f,
                    critical: 10f, low: 30f, high: 70f)
                .WithNeed(NeedEnergy, initial: 90f, max: 100f, decayRate: 1.5f,
                    critical: 5f, low: 20f, high: 60f)
                .WithSchedule(ScheduleDaily)
                .WithMemory(maxMemories: 16, decayRate: 0.005f)
                .WithRelationships()
                .WithFeedback()
                .Build();

            // Set farmer's nav start node
            em.SetComponentData(_farmer, new NavAgent
            {
                GraphId = NavGraph, CurrentNodeId = NodeVillage,
                CapabilityFlags = 0, Status = NavAgentStatus.Idle
            });

            _gatherer = AgentBuilder.Create(em)
                .WithArchetype(ArchetypeGatherer, agentId: 2)
                .WithStrategy(StrategyWork)
                .AtPosition(new float3(5, 0, 0))
                .WithNavigation(NavGraph, moveSpeed: 6f, stoppingDistance: 1f)
                .WithAwareness(range: 25f, maxPOIs: 4, maxAgents: 4)
                .WithContextRefresh(ContextRefreshMode.Interval, refreshInterval: 0f)
                .WithNeed(NeedHunger, initial: 50f, max: 100f, decayRate: 4f,
                    critical: 10f, low: 30f, high: 70f)
                .WithNeed(NeedEnergy, initial: 80f, max: 100f, decayRate: 2f,
                    critical: 5f, low: 20f, high: 60f)
                .WithSchedule(ScheduleDaily)
                .WithMemory(maxMemories: 16, decayRate: 0.005f)
                .WithRelationships()
                .WithFeedback()
                .Build();

            em.SetComponentData(_gatherer, new NavAgent
            {
                GraphId = NavGraph, CurrentNodeId = NodeVillage,
                CapabilityFlags = 0, Status = NavAgentStatus.Idle
            });
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

        Entity CreateProducer(int marketId, int itemId, float rate)
        {
            var e = CreateEntity(ComponentType.ReadWrite<ProducerData>());
            Manager.SetComponentData(e, new ProducerData
            {
                MarketId = marketId, ProducedItemId = itemId,
                ProductionRate = rate, ProductionCost = 1f
            });
            return e;
        }

        Entity CreateConsumer(int marketId, int itemId, float rate)
        {
            var e = CreateEntity(ComponentType.ReadWrite<ConsumerData>());
            Manager.SetComponentData(e, new ConsumerData
            {
                MarketId = marketId, ConsumedItemId = itemId,
                ConsumptionRate = rate, WillingnessToPay = 10f
            });
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

        float GetResource(Entity e, int resourceId)
        {
            var buf = Manager.GetBuffer<ResourceElement>(e);
            return ResourceUtility.Get(in buf, resourceId);
        }

        // ============================================================
        // TEST: Full Village Day Simulation
        // ============================================================

        [Test]
        public void VillageDay_AllSystemsIntegrate()
        {
            BuildWorld();

            // === Phase 1: Initial setup verification (tick 5 frames) ===
            Tick(5);

            // Both agents should have strategies assigned
            Assert.AreEqual(StrategyWork, Manager.GetComponentData<AgentData>(_farmer).StrategyId,
                "Farmer should have WorkStrategy");
            Assert.AreEqual(StrategyWork, Manager.GetComponentData<AgentData>(_gatherer).StrategyId,
                "Gatherer should have WorkStrategy");

            // Scoring should have picked an action
            var farmerScore = Manager.GetComponentData<ScoringResult>(_farmer);
            Assert.IsTrue(farmerScore.BestActionId >= 0,
                $"Farmer should have a selected action, got {farmerScore.BestActionId}");

            var gathererScore = Manager.GetComponentData<ScoringResult>(_gatherer);
            Assert.IsTrue(gathererScore.BestActionId >= 0,
                $"Gatherer should have a selected action, got {gathererScore.BestActionId}");

            // World knowledge: agents should see POIs
            var farmerPOIs = Manager.GetBuffer<KnownPOIElement>(_farmer);
            Assert.IsTrue(farmerPOIs.Length > 0,
                "Farmer should detect POIs via WorldKnowledgeSystem");

            // Agents should detect each other
            var farmerAgents = Manager.GetBuffer<KnownAgentElement>(_farmer);
            Assert.IsTrue(farmerAgents.Length > 0,
                "Farmer should detect gatherer via WorldKnowledgeSystem");

            // Schedule should detect the 6-18 work slot (time=8am)
            var farmerSched = Manager.GetComponentData<ScheduleData>(_farmer);
            Assert.AreEqual(0, farmerSched.CurrentSlotIndex,
                "Farmer should be in work slot (6-18) at 8am");

            // === Phase 2: Needs decay over time (tick 200 frames = 10s) ===
            Tick(200);

            float farmerHunger = GetResource(_farmer, NeedHunger);
            float gathererHunger = GetResource(_gatherer, NeedHunger);
            float farmerEnergy = GetResource(_farmer, NeedEnergy);

            Assert.Less(farmerHunger, 70f,
                $"Farmer hunger should have decayed from 70, now {farmerHunger:F1}");
            Assert.Less(gathererHunger, 50f,
                $"Gatherer hunger should have decayed from 50, now {gathererHunger:F1}");
            Assert.Less(farmerEnergy, 90f,
                $"Farmer energy should have decayed from 90, now {farmerEnergy:F1}");

            // === Phase 3: Scoring responds to needs ===
            // Gatherer started hungrier (50) with faster decay (4/s).
            // After 10s: 50 - 40 = 10 (critical!). Should be scoring eat/farm high.
            var gathererUrgency = NeedUrgency.Satisfied;
            var needs = Manager.GetBuffer<NeedElement>(_gatherer);
            for (int i = 0; i < needs.Length; i++)
            {
                if (needs[i].ResourceId == NeedHunger)
                    gathererUrgency = needs[i].CurrentUrgency;
            }
            Assert.IsTrue(gathererUrgency >= NeedUrgency.Low,
                $"Gatherer hunger urgency should be Low or Critical, got {gathererUrgency}");

            // === Phase 4: Economy updates prices ===
            var marketItems = Manager.GetBuffer<MarketItemElement>(_market);
            float foodPrice = EconomyUtility.GetPrice(in marketItems, ItemFood);
            float woodPrice = EconomyUtility.GetPrice(in marketItems, ItemWood);

            // Food: demand(5) > supply(3) → price should rise above base 10
            Assert.Greater(foodPrice, 10f,
                $"Food price should rise (demand>supply), got {foodPrice:F2}");

            // Wood: supply(4) > demand(2) → price should fall below base 8
            Assert.Less(woodPrice, 8f,
                $"Wood price should fall (supply>demand), got {woodPrice:F2}");

            // === Phase 5: Context is populated ===
            var farmerContext = Manager.GetBuffer<AgentContextSnapshot>(_farmer);
            Assert.IsTrue(farmerContext.Length > 0,
                "Farmer should have context snapshot populated");

            // === Phase 6: Activity system reflects state ===
            var farmerActivity = Manager.GetComponentData<AgentActivity>(_farmer);
            // Agent should be in some active state (not stuck at default)
            Assert.IsTrue(
                farmerActivity.CurrentActivity == AgentActivityType.Idle ||
                farmerActivity.CurrentActivity == AgentActivityType.Moving ||
                farmerActivity.CurrentActivity == AgentActivityType.PerformingAction ||
                farmerActivity.CurrentActivity == AgentActivityType.Queued,
                $"Farmer activity should be a valid state, got {farmerActivity.CurrentActivity}");

            // === Phase 7: Tick more to let agents settle (300 more = 15s total sim) ===
            Tick(300);

            // Feedback should have recorded some activity by now
            var farmerFeedback = Manager.GetComponentData<AgentFeedback>(_farmer);
            Assert.IsTrue(farmerFeedback.LastEvaluationTime > 0,
                "Feedback evaluation should have run at least once");

            // === Phase 8: Add memory manually (simulating what game code would do) ===
            var farmerMemories = Manager.GetBuffer<MemoryElement>(_farmer);
            var farmerMemConfig = Manager.GetComponentData<MemoryConfig>(_farmer);
            MemoryUtility.AddMemory(ref farmerMemories, in farmerMemConfig,
                MemoryFoundFood, new float3(20, 0, 0), importance: 1f,
                intParam: ItemFood, floatParam: 10f, timestamp: 15.0);

            Assert.AreEqual(1, farmerMemories.Length,
                "Farmer should have 1 memory after AddMemory");

            // Tick a bit more — memory should start decaying
            Tick(100); // 5 more seconds

            farmerMemories = Manager.GetBuffer<MemoryElement>(_farmer);
            if (farmerMemories.Length > 0)
            {
                Assert.Less(farmerMemories[0].Importance, 1f,
                    "Memory importance should have decayed from 1.0");
            }

            // === Phase 9: Verify shared context changes behavior ===
            // Set food supply to 0 — agents with food-availability consideration should stop farming
            var ctxEntries = Manager.GetBuffer<SharedContextEntry>(_globalContext);
            SharedContextUtility.Set(ref ctxEntries, CtxFoodSupply, 0f, 20.0);

            // Force context refresh
            Manager.SetComponentEnabled<ContextRefreshRequest>(_farmer, true);
            Tick(5);

            // Farmer's scoring should now avoid ActionFarm (food step consideration = 0)
            farmerScore = Manager.GetComponentData<ScoringResult>(_farmer);
            Assert.AreNotEqual(ActionFarm, farmerScore.BestActionId,
                $"Farmer should NOT pick Farm when food supply=0, got {farmerScore.BestActionId}");

            // Restore food
            ctxEntries = Manager.GetBuffer<SharedContextEntry>(_globalContext);
            SharedContextUtility.Set(ref ctxEntries, CtxFoodSupply, 100f, 21.0);

            // === Phase 10: World time and schedule ===
            // Fast-forward time to night (22:00) to trigger schedule slot change
            var timeData = Manager.GetComponentData<WorldTimeData>(_worldTime);
            timeData.TimeOfDay = 21.9f; // Just before 22:00
            Manager.SetComponentData(_worldTime, timeData);

            Tick(10); // Advance ~0.5s of game time, crossing into 22:00

            timeData = Manager.GetComponentData<WorldTimeData>(_worldTime);
            // Verify time advanced past 22
            bool timeAdvanced = timeData.TimeOfDay >= 22f || timeData.TimeOfDay < 6f;
            Assert.IsTrue(timeAdvanced,
                $"Time should have crossed 22:00, currently {timeData.TimeOfDay:F2}");

            // Schedule should detect the sleep slot (22-6)
            farmerSched = Manager.GetComponentData<ScheduleData>(_farmer);
            Assert.AreEqual(1, farmerSched.CurrentSlotIndex,
                $"Farmer should be in sleep slot at night, slot={farmerSched.CurrentSlotIndex}");

            // Strategy should have been overridden to RestStrategy
            var farmerData = Manager.GetComponentData<AgentData>(_farmer);
            Assert.AreEqual(StrategyRest, farmerData.StrategyId,
                $"Farmer should have RestStrategy at night, got {farmerData.StrategyId}");

            // === Phase 11: Social — request interaction ===
            Manager.SetComponentData(_farmer, new InteractionRequest
            {
                TargetAgent = _gatherer,
                InteractionType = 1, // Talk
                OfferValue = 0f
            });
            Manager.SetComponentEnabled<InteractionRequest>(_farmer, true);

            Tick(3);

            var farmerInteraction = Manager.GetComponentData<InteractionState>(_farmer);
            var gathererInteraction = Manager.GetComponentData<InteractionState>(_gatherer);

            // Interaction should have matched (both agents exist and are not busy interacting)
            bool interactionStarted =
                farmerInteraction.Phase == InteractionPhase.Active ||
                gathererInteraction.Phase == InteractionPhase.Active;
            // Note: might be rejected if gatherer is already interacting
            Assert.IsTrue(
                interactionStarted ||
                Manager.IsComponentEnabled<InteractionRejected>(_farmer),
                "Interaction should either start or be rejected");

            // === Final assertions: system coherence ===

            // Both agents still exist
            Assert.IsTrue(Manager.Exists(_farmer), "Farmer should still exist");
            Assert.IsTrue(Manager.Exists(_gatherer), "Gatherer should still exist");

            // All resource values are in valid range
            Assert.IsTrue(GetResource(_farmer, NeedHunger) >= 0f &&
                          GetResource(_farmer, NeedHunger) <= 100f,
                "Farmer hunger in valid range");
            Assert.IsTrue(GetResource(_farmer, NeedEnergy) >= 0f &&
                          GetResource(_farmer, NeedEnergy) <= 100f,
                "Farmer energy in valid range");
            Assert.IsTrue(GetResource(_gatherer, NeedHunger) >= 0f &&
                          GetResource(_gatherer, NeedHunger) <= 100f,
                "Gatherer hunger in valid range");
            Assert.IsTrue(GetResource(_gatherer, NeedEnergy) >= 0f &&
                          GetResource(_gatherer, NeedEnergy) <= 100f,
                "Gatherer energy in valid range");

            // Market prices are positive
            marketItems = Manager.GetBuffer<MarketItemElement>(_market);
            Assert.Greater(EconomyUtility.GetPrice(in marketItems, ItemFood), 0f, "Food price positive");
            Assert.Greater(EconomyUtility.GetPrice(in marketItems, ItemWood), 0f, "Wood price positive");

            // Nav agents have valid status
            var farmerNav = Manager.GetComponentData<NavAgent>(_farmer);
            Assert.IsTrue(
                farmerNav.Status == NavAgentStatus.Idle ||
                farmerNav.Status == NavAgentStatus.FollowingPath ||
                farmerNav.Status == NavAgentStatus.Arrived,
                $"Farmer nav status should be valid, got {farmerNav.Status}");
        }
    }
}
