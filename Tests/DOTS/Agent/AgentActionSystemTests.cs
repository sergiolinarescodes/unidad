using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests for AgentActionSystem: phase transitions, effect application,
    /// action events, completion records, and scoring→action bridging.
    /// </summary>
    [TestFixture]
    public class AgentActionSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        const int StrategyId = 1;
        const int ActionGather = 10;
        const int ActionRest = 11;
        const int ActionTypeGather = 40;
        const int ActionTypeRest = 41;
        const int NeedHunger = 1;

        public override void SetUp()
        {
            base.SetUp();
            _nextTime = 0.1;

            var actionClear = GetOrCreateSystem<ActionEventClearSystem>();
            var strategyAssign = GetOrCreateSystem<StrategyAssignmentSystem>();
            var scoring = GetOrCreateSystem<ScoringSystem>();
            var actionQueue = GetOrCreateSystem<ActionQueueSystem>();
            var actionSystem = GetOrCreateSystem<AgentActionSystem>();
            _group = CreateSimGroup(actionClear, strategyAssign, scoring, actionQueue, actionSystem);
        }

        Entity CreateStrategy()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<StrategyDefinition>(),
                ComponentType.ReadWrite<StrategyActionPlanEntry>());

            Manager.SetComponentData(e, new StrategyDefinition { StrategyId = StrategyId });

            var actions = AddBuffer<StrategyActionElement>(e);
            actions.Add(new StrategyActionElement
            {
                ActionId = ActionGather, ActionType = ActionTypeGather, PreconditionFlags = 0
            });
            actions.Add(new StrategyActionElement
            {
                ActionId = ActionRest, ActionType = ActionTypeRest, PreconditionFlags = 0
            });

            var cons = AddBuffer<StrategyConsiderationTemplate>(e);
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionGather, InputType = ScoringInputType.NeedLevel,
                InputParam = NeedHunger, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionRest, InputType = ScoringInputType.Constant,
                InputParam = 10, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });

            var effects = AddBuffer<StrategyActionEffectTemplate>(e);
            effects.Add(new StrategyActionEffectTemplate
            {
                ActionId = ActionGather,
                EffectType = ActionEffectType.AddToResource,
                TargetResourceId = NeedHunger,
                Value = 40f
            });

            AddBuffer<StrategyParamElement>(e);
            return e;
        }

        Entity CreateActionAgent(float hunger)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentData>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<AgentLocomotion>(),
                ComponentType.ReadWrite<AgentActivity>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>(),
                ComponentType.ReadWrite<ScoringResult>(),
                ComponentType.ReadWrite<ActionSelectionChanged>(),
                ComponentType.ReadWrite<StrategyAssignRequest>(),
                ComponentType.ReadWrite<StrategyAssigned>(),
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<AgentPreconditions>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>(),
                ComponentType.ReadWrite<ActionQueueConfig>(),
                ComponentType.ReadWrite<ActionQueueProgress>(),
                ComponentType.ReadWrite<NeedUrgencyChanged>(),
                ComponentType.ReadWrite<StateMachineData>(),
                ComponentType.ReadWrite<StateEntered>(),
                ComponentType.ReadWrite<StateExited>(),
                ComponentType.ReadWrite<QueueAdvanced>(),
                ComponentType.ReadWrite<QueueCompleted>(),
                ComponentType.ReadWrite<QueueInterrupted>(),
                ComponentType.ReadWrite<ResourceChanged>(),
                ComponentType.ReadWrite<ResourceDepleted>(),
                ComponentType.ReadWrite<ResourceFilled>(),
                ComponentType.ReadWrite<ContextRefreshPolicy>(),
                ComponentType.ReadWrite<ContextRefreshRequest>(),
                ComponentType.ReadWrite<ContextRefreshed>(),
                ComponentType.ReadWrite<AgentSpawned>(),
                ComponentType.ReadWrite<AgentActivated>(),
                ComponentType.ReadWrite<AgentSuspended>(),
                ComponentType.ReadWrite<AgentDespawning>(),
                ComponentType.ReadWrite<ActivityChanged>());

            Manager.SetComponentData(e, new AgentData { StrategyId = -1 });
            Manager.SetComponentData(e, new AgentActionState { CurrentActionId = -1 });
            Manager.SetComponentData(e, ActionQueueConfig.Default);
            Manager.SetComponentData(e, new ScoringResult { BestActionId = -1, PreviousBestActionId = -1 });
            Manager.SetComponentData(e, LocalTransform.FromPosition(float3.zero));

            SetEnabled<ActionSelectionChanged>(e, false);
            SetEnabled<StrategyAssigned>(e, false);
            SetEnabled<NeedUrgencyChanged>(e, false);
            SetEnabled<ActionStarted>(e, false);
            SetEnabled<ActionCompleted>(e, false);
            SetEnabled<ActionInterrupted>(e, false);
            SetEnabled<QueueAdvanced>(e, false);
            SetEnabled<QueueCompleted>(e, false);
            SetEnabled<QueueInterrupted>(e, false);
            SetEnabled<ContextRefreshRequest>(e, false);
            SetEnabled<ContextRefreshed>(e, false);
            SetEnabled<AgentSpawned>(e, false);
            SetEnabled<AgentActivated>(e, false);
            SetEnabled<AgentSuspended>(e, false);
            SetEnabled<AgentDespawning>(e, false);
            SetEnabled<ActivityChanged>(e, false);
            SetEnabled<ResourceChanged>(e, false);
            SetEnabled<ResourceDepleted>(e, false);
            SetEnabled<ResourceFilled>(e, false);

            // Resources
            var resources = AddBuffer<ResourceElement>(e);
            resources.Add(new ResourceElement
            {
                ResourceId = NeedHunger, CurrentValue = hunger,
                InitialValue = hunger, BaseMin = 0f, BaseMax = 100f
            });
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
            AddBuffer<NeedDecayModifier>(e);
            AddBuffer<NeedUrgencyChangeRecord>(e);

            // Scoring + Strategy buffers
            AddBuffer<ConsiderationElement>(e);
            AddBuffer<ActionTimestampElement>(e);
            AddBuffer<StrategyParamElement>(e);
            AddBuffer<AgentContextSnapshot>(e);
            AddBuffer<ActionEffectElement>(e);
            AddBuffer<ActionCompletionRecord>(e);
            AddBuffer<ActionQueueEntry>(e);

            // Request strategy assignment
            Manager.SetComponentData(e, new StrategyAssignRequest { StrategyId = StrategyId });
            SetEnabled<StrategyAssignRequest>(e, true);

            return e;
        }

        double _nextTime = 0.1;

        void Tick()
        {
            SetWorldTime(_nextTime, 0.1f);
            _nextTime += 0.1;
            UpdateGroup(_group);
        }

        [Test]
        public void NewActionSelection_SetsPhaseToStarting()
        {
            CreateStrategy();
            var agent = CreateActionAgent(20f); // hungry → picks Gather

            Tick();

            var actionState = Manager.GetComponentData<AgentActionState>(agent);
            Assert.AreEqual(ActionGather, actionState.CurrentActionId);
            Assert.AreEqual(ActionTypeGather, actionState.CurrentActionType);
            Assert.AreEqual(AgentActionPhase.Starting, actionState.Phase);
        }

        [Test]
        public void NewAction_FiresActionStartedEvent()
        {
            CreateStrategy();
            var agent = CreateActionAgent(20f);

            Tick();

            Assert.IsTrue(IsEnabled<ActionStarted>(agent), "ActionStarted should fire");
        }

        [Test]
        public void NewAction_PopulatesEffectsFromStrategy()
        {
            CreateStrategy();
            var agent = CreateActionAgent(20f);

            Tick();

            var effects = Manager.GetBuffer<ActionEffectElement>(agent);
            Assert.AreEqual(1, effects.Length, "Should have 1 effect from strategy template");
            Assert.AreEqual(ActionEffectType.AddToResource, effects[0].EffectType);
            Assert.AreEqual(NeedHunger, effects[0].TargetResourceId);
            Assert.AreEqual(40f, effects[0].Value, 0.01f);
        }

        [Test]
        public void ActionEventsCleared_NextFrame()
        {
            CreateStrategy();
            var agent = CreateActionAgent(20f);

            // Frame 1: action starts, ActionStarted fires
            Tick();
            Assert.IsTrue(IsEnabled<ActionStarted>(agent), "ActionStarted should fire on frame 1");

            // Frame 2: ActionEventClearSystem clears the event
            Tick();

            Assert.IsFalse(IsEnabled<ActionStarted>(agent), "ActionStarted should be cleared next frame");
        }

        [Test]
        public void ActionSwitch_InterruptsPreviousAction()
        {
            CreateStrategy();
            var agent = CreateActionAgent(20f); // hungry → Gather

            Tick();
            Assert.AreEqual(ActionGather, Manager.GetComponentData<AgentActionState>(agent).CurrentActionId);

            // Make hunger satisfied so Rest constant wins
            var resources = Manager.GetBuffer<ResourceElement>(agent);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(agent);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(agent);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(agent);
            ResourceUtility.Set(ref resources, ref changes, in maxMods, in minMods, NeedHunger, 95f);

            // Update needs urgency manually
            var needs = Manager.GetBuffer<NeedElement>(agent);
            var n = needs[0];
            n.CurrentUrgency = NeedUtility.EvaluateUrgency(95f, 10f, 30f, 70f);
            needs[0] = n;

            Tick();

            var actionState = Manager.GetComponentData<AgentActionState>(agent);
            Assert.AreEqual(ActionRest, actionState.CurrentActionId,
                $"Should switch to Rest, got actionId={actionState.CurrentActionId}");
            Assert.IsTrue(IsEnabled<ActionInterrupted>(agent),
                "ActionInterrupted should fire when switching actions");
        }

        [Test]
        public void ActionSwitch_CreatesFailedCompletionRecord()
        {
            CreateStrategy();
            var agent = CreateActionAgent(20f);

            Tick();

            // Switch by changing resources
            var resources = Manager.GetBuffer<ResourceElement>(agent);
            var changes = Manager.GetBuffer<ResourceChangeRecord>(agent);
            var maxMods = Manager.GetBuffer<ResourceMaxModifier>(agent);
            var minMods = Manager.GetBuffer<ResourceMinModifier>(agent);
            ResourceUtility.Set(ref resources, ref changes, in maxMods, in minMods, NeedHunger, 95f);

            var needs = Manager.GetBuffer<NeedElement>(agent);
            var n = needs[0];
            n.CurrentUrgency = NeedUtility.EvaluateUrgency(95f, 10f, 30f, 70f);
            needs[0] = n;

            Tick();

            var records = Manager.GetBuffer<ActionCompletionRecord>(agent);
            Assert.IsTrue(records.Length >= 1, "Should have a completion record for interrupted action");
            Assert.AreEqual(ActionGather, records[0].ActionId);
            Assert.IsFalse(records[0].WasSuccessful, "Interrupted action should not be successful");
        }

        [Test]
        public void ActionEffectApply_AddToResource()
        {
            var resources = Manager.CreateEntity(
                ComponentType.ReadWrite<ResourceElement>(),
                ComponentType.ReadWrite<ResourceChangeRecord>(),
                ComponentType.ReadWrite<ResourceMaxModifier>(),
                ComponentType.ReadWrite<ResourceMinModifier>());

            var resBuf = AddBuffer<ResourceElement>(resources);
            resBuf.Add(new ResourceElement
            {
                ResourceId = NeedHunger, CurrentValue = 30f, BaseMin = 0f, BaseMax = 100f
            });
            var changesBuf = Manager.GetBuffer<ResourceChangeRecord>(resources);
            var maxModsBuf = Manager.GetBuffer<ResourceMaxModifier>(resources);
            var minModsBuf = Manager.GetBuffer<ResourceMinModifier>(resources);
            var sm = new StateMachineData();

            var effect = new ActionEffectElement
            {
                EffectType = ActionEffectType.AddToResource,
                TargetResourceId = NeedHunger,
                Value = 25f
            };

            AgentActionUtility.ApplyEffect(in effect, ref resBuf, ref changesBuf,
                in maxModsBuf, in minModsBuf, ref sm);

            float value = ResourceUtility.Get(in resBuf, NeedHunger);
            Assert.AreEqual(55f, value, 0.01f, "Should add 25 to hunger (30+25=55)");
        }

        [Test]
        public void ActionEffectApply_SetResource()
        {
            var resources = Manager.CreateEntity(
                ComponentType.ReadWrite<ResourceElement>(),
                ComponentType.ReadWrite<ResourceChangeRecord>(),
                ComponentType.ReadWrite<ResourceMaxModifier>(),
                ComponentType.ReadWrite<ResourceMinModifier>());

            var resBuf = AddBuffer<ResourceElement>(resources);
            resBuf.Add(new ResourceElement
            {
                ResourceId = NeedHunger, CurrentValue = 30f, BaseMin = 0f, BaseMax = 100f
            });
            var changesBuf = Manager.GetBuffer<ResourceChangeRecord>(resources);
            var maxModsBuf = Manager.GetBuffer<ResourceMaxModifier>(resources);
            var minModsBuf = Manager.GetBuffer<ResourceMinModifier>(resources);
            var sm = new StateMachineData();

            var effect = new ActionEffectElement
            {
                EffectType = ActionEffectType.SetResource,
                TargetResourceId = NeedHunger,
                Value = 80f
            };

            AgentActionUtility.ApplyEffect(in effect, ref resBuf, ref changesBuf,
                in maxModsBuf, in minModsBuf, ref sm);

            float value = ResourceUtility.Get(in resBuf, NeedHunger);
            Assert.AreEqual(80f, value, 0.01f);
        }

        [Test]
        public void ActionEffectApply_TriggerState()
        {
            var resources = Manager.CreateEntity(
                ComponentType.ReadWrite<ResourceElement>(),
                ComponentType.ReadWrite<ResourceChangeRecord>(),
                ComponentType.ReadWrite<ResourceMaxModifier>(),
                ComponentType.ReadWrite<ResourceMinModifier>());

            var resBuf = AddBuffer<ResourceElement>(resources);
            var changesBuf = Manager.GetBuffer<ResourceChangeRecord>(resources);
            var maxModsBuf = Manager.GetBuffer<ResourceMaxModifier>(resources);
            var minModsBuf = Manager.GetBuffer<ResourceMinModifier>(resources);
            var sm = new StateMachineData { CurrentState = 0, TransitionRequested = false };

            var effect = new ActionEffectElement
            {
                EffectType = ActionEffectType.TriggerState,
                Value = 5f
            };

            AgentActionUtility.ApplyEffect(in effect, ref resBuf, ref changesBuf,
                in maxModsBuf, in minModsBuf, ref sm);

            Assert.IsTrue(sm.TransitionRequested);
            Assert.AreEqual(5, sm.RequestedState);
        }

        [Test]
        public void ActionEffectApply_AllEffectsInBuffer()
        {
            var resources = Manager.CreateEntity(
                ComponentType.ReadWrite<ResourceElement>(),
                ComponentType.ReadWrite<ResourceChangeRecord>(),
                ComponentType.ReadWrite<ResourceMaxModifier>(),
                ComponentType.ReadWrite<ResourceMinModifier>(),
                ComponentType.ReadWrite<ActionEffectElement>());

            var resBuf = AddBuffer<ResourceElement>(resources);
            resBuf.Add(new ResourceElement
            {
                ResourceId = 1, CurrentValue = 20f, BaseMin = 0f, BaseMax = 100f
            });
            resBuf.Add(new ResourceElement
            {
                ResourceId = 2, CurrentValue = 50f, BaseMin = 0f, BaseMax = 100f
            });
            var changesBuf = Manager.GetBuffer<ResourceChangeRecord>(resources);
            var maxModsBuf = Manager.GetBuffer<ResourceMaxModifier>(resources);
            var minModsBuf = Manager.GetBuffer<ResourceMinModifier>(resources);

            var effectsBuf = Manager.GetBuffer<ActionEffectElement>(resources);
            effectsBuf.Add(new ActionEffectElement
            {
                EffectType = ActionEffectType.AddToResource, TargetResourceId = 1, Value = 30f
            });
            effectsBuf.Add(new ActionEffectElement
            {
                EffectType = ActionEffectType.SetResource, TargetResourceId = 2, Value = 10f
            });

            var sm = new StateMachineData();
            AgentActionUtility.ApplyAllEffects(in effectsBuf, ref resBuf, ref changesBuf,
                in maxModsBuf, in minModsBuf, ref sm);

            Assert.AreEqual(50f, ResourceUtility.Get(in resBuf, 1), 0.01f, "20+30=50");
            Assert.AreEqual(10f, ResourceUtility.Get(in resBuf, 2), 0.01f, "Set to 10");
        }

        [Test]
        public void PreconditionFlags_BlocksAction()
        {
            // Create strategy with a precondition on Gather
            var stratEntity = CreateEntity(
                ComponentType.ReadWrite<StrategyDefinition>(),
                ComponentType.ReadWrite<StrategyActionPlanEntry>());

            Manager.SetComponentData(stratEntity, new StrategyDefinition { StrategyId = StrategyId });

            var actions = AddBuffer<StrategyActionElement>(stratEntity);
            actions.Add(new StrategyActionElement
            {
                ActionId = ActionGather, ActionType = ActionTypeGather,
                PreconditionFlags = 0x01 // Requires flag bit 0
            });
            actions.Add(new StrategyActionElement
            {
                ActionId = ActionRest, ActionType = ActionTypeRest,
                PreconditionFlags = 0 // No preconditions
            });

            var cons = AddBuffer<StrategyConsiderationTemplate>(stratEntity);
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionGather, InputType = ScoringInputType.NeedLevel,
                InputParam = NeedHunger, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });
            cons.Add(new StrategyConsiderationTemplate
            {
                ActionId = ActionRest, InputType = ScoringInputType.Constant,
                InputParam = 10, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });

            AddBuffer<StrategyActionEffectTemplate>(stratEntity);
            AddBuffer<StrategyParamElement>(stratEntity);

            // Agent is hungry but does NOT have precondition flag 0x01
            var agent = CreateActionAgent(15f);
            Manager.SetComponentData(agent, new AgentPreconditions { AvailableFlags = 0 });

            Tick();

            var actionState = Manager.GetComponentData<AgentActionState>(agent);
            // Scoring picks Gather (highest score), but precondition check fails at execution.
            // AgentActionSystem does not fall through to second-best action — agent stays idle.
            Assert.AreEqual(-1, actionState.CurrentActionId,
                $"Blocked action should leave agent with no action, got {actionState.CurrentActionId}");
            Assert.AreEqual(AgentActionPhase.None, actionState.Phase,
                "Blocked action should not start any phase");
            Assert.IsFalse(IsEnabled<ActionStarted>(agent),
                "ActionStarted should not fire when preconditions block");
        }
    }
}
