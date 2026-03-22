using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests for ActionQueueSystem — queue advancement, interrupt policies,
    /// QueueFromStrategy plan population, QueueManual enqueue.
    /// </summary>
    [TestFixture]
    public class ActionQueueSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;
        double _nextTime;

        public override void SetUp()
        {
            base.SetUp();
            _nextTime = 0.1;

            // No ActionEventClearSystem — we manually set ActionCompleted before Tick
            // and need ActionQueueSystem to see it in the same frame.
            var strategyAssign = GetOrCreateSystem<StrategyAssignmentSystem>();
            var scoring = GetOrCreateSystem<ScoringSystem>();
            var actionSystem = GetOrCreateSystem<AgentActionSystem>();
            var actionQueue = GetOrCreateSystem<ActionQueueSystem>();
            _group = CreateSimGroup(strategyAssign, scoring, actionSystem, actionQueue);
        }

        void Tick()
        {
            SetWorldTime(_nextTime, 0.1f);
            _nextTime += 0.1;
            UpdateGroup(_group);
        }

        Entity CreateQueueAgent(ActionQueueMode mode, InterruptPolicy policy = InterruptPolicy.Immediate)
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
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<AgentPreconditions>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>(),
                ComponentType.ReadWrite<ActionQueueConfig>(),
                ComponentType.ReadWrite<ActionQueueProgress>(),
                ComponentType.ReadWrite<QueueAdvanced>(),
                ComponentType.ReadWrite<QueueCompleted>(),
                ComponentType.ReadWrite<QueueInterrupted>(),
                ComponentType.ReadWrite<StateMachineData>(),
                ComponentType.ReadWrite<StateEntered>(),
                ComponentType.ReadWrite<StateExited>(),
                ComponentType.ReadWrite<NeedUrgencyChanged>(),
                ComponentType.ReadWrite<ForceRescoreTag>());

            Manager.SetComponentData(e, new AgentData { StrategyId = -1, ArchetypeId = 1 });
            Manager.SetComponentData(e, new AgentActionState { CurrentActionId = -1 });
            Manager.SetComponentData(e, new ActionQueueConfig
            {
                Mode = mode,
                InterruptPolicy = policy,
                AllowRescore = true
            });
            Manager.SetComponentData(e, new ScoringResult { BestActionId = -1, PreviousBestActionId = -1 });
            Manager.SetComponentData(e, LocalTransform.FromPosition(float3.zero));

            // Disable all enableable tags
            SetEnabled<ActionSelectionChanged>(e, false);
            SetEnabled<StrategyAssignRequest>(e, false);
            SetEnabled<StrategyAssigned>(e, false);
            SetEnabled<ActionStarted>(e, false);
            SetEnabled<ActionCompleted>(e, false);
            SetEnabled<ActionInterrupted>(e, false);
            SetEnabled<QueueAdvanced>(e, false);
            SetEnabled<QueueCompleted>(e, false);
            SetEnabled<QueueInterrupted>(e, false);
            SetEnabled<NeedUrgencyChanged>(e, false);
            SetEnabled<StateEntered>(e, false);
            SetEnabled<StateExited>(e, false);
            SetEnabled<ForceRescoreTag>(e, false);

            // Buffers
            AddBuffer<ResourceElement>(e);
            AddBuffer<ResourceChangeRecord>(e);
            AddBuffer<ResourceMaxModifier>(e);
            AddBuffer<ResourceMinModifier>(e);
            AddBuffer<NeedElement>(e);
            AddBuffer<NeedDecayModifier>(e);
            AddBuffer<NeedUrgencyChangeRecord>(e);
            AddBuffer<ConsiderationElement>(e);
            AddBuffer<ActionTimestampElement>(e);
            AddBuffer<StrategyParamElement>(e);
            AddBuffer<AgentContextSnapshot>(e);
            AddBuffer<ActionEffectElement>(e);
            AddBuffer<ActionCompletionRecord>(e);
            AddBuffer<ActionQueueEntry>(e);

            return e;
        }

        // === QueueManual Tests ===

        [Test]
        public void ManualQueue_AdvancesOnActionCompleted()
        {
            var agent = CreateQueueAgent(ActionQueueMode.QueueManual);

            // Manually enqueue 2 actions
            var queue = Manager.GetBuffer<ActionQueueEntry>(agent);
            var progress = Manager.GetComponentData<ActionQueueProgress>(agent);
            ActionQueueUtility.Enqueue(ref queue, ref progress, actionId: 10, actionType: 40);
            ActionQueueUtility.Enqueue(ref queue, ref progress, actionId: 11, actionType: 41);
            Manager.SetComponentData(agent, progress);

            // Set first entry as active with Starting phase
            var first = queue[0];
            first.Status = ActionQueueEntryStatus.Active;
            queue[0] = first;
            Manager.SetComponentData(agent, new AgentActionState
            {
                CurrentActionId = 10, CurrentActionType = 40, Phase = AgentActionPhase.Executing
            });

            // Simulate ActionCompleted
            SetEnabled<ActionCompleted>(agent, true);

            Tick();

            // Should advance to second entry
            var prog = Manager.GetComponentData<ActionQueueProgress>(agent);
            Assert.AreEqual(1, prog.CurrentIndex, "Should advance to index 1");

            var action = Manager.GetComponentData<AgentActionState>(agent);
            Assert.AreEqual(11, action.CurrentActionId, "Should switch to action 11");
            Assert.AreEqual(AgentActionPhase.Starting, action.Phase);
        }

        [Test]
        public void ManualQueue_FiresQueueCompleted_WhenAllDone()
        {
            var agent = CreateQueueAgent(ActionQueueMode.QueueManual);

            // Single entry queue
            var queue = Manager.GetBuffer<ActionQueueEntry>(agent);
            var progress = Manager.GetComponentData<ActionQueueProgress>(agent);
            ActionQueueUtility.Enqueue(ref queue, ref progress, actionId: 10, actionType: 40);
            Manager.SetComponentData(agent, progress);

            var first = queue[0];
            first.Status = ActionQueueEntryStatus.Active;
            queue[0] = first;
            Manager.SetComponentData(agent, new AgentActionState
            {
                CurrentActionId = 10, CurrentActionType = 40, Phase = AgentActionPhase.Executing
            });

            SetEnabled<ActionCompleted>(agent, true);

            Tick();

            Assert.IsTrue(IsEnabled<QueueCompleted>(agent), "QueueCompleted should fire");
            queue = Manager.GetBuffer<ActionQueueEntry>(agent);
            Assert.AreEqual(0, queue.Length, "Queue should be cleared after completion");
        }

        [Test]
        public void ManualQueue_FiresQueueAdvanced_OnStepTransition()
        {
            var agent = CreateQueueAgent(ActionQueueMode.QueueManual);

            var queue = Manager.GetBuffer<ActionQueueEntry>(agent);
            var progress = Manager.GetComponentData<ActionQueueProgress>(agent);
            ActionQueueUtility.Enqueue(ref queue, ref progress, actionId: 10, actionType: 40);
            ActionQueueUtility.Enqueue(ref queue, ref progress, actionId: 11, actionType: 41);
            Manager.SetComponentData(agent, progress);

            var first = queue[0];
            first.Status = ActionQueueEntryStatus.Active;
            queue[0] = first;
            Manager.SetComponentData(agent, new AgentActionState
            {
                CurrentActionId = 10, CurrentActionType = 40, Phase = AgentActionPhase.Executing
            });

            SetEnabled<ActionCompleted>(agent, true);

            Tick();

            Assert.IsTrue(IsEnabled<QueueAdvanced>(agent), "QueueAdvanced should fire on step transition");
        }

        // === Interrupt Policy Tests ===

        [Test]
        public void InterruptImmediate_ClearsQueueOnNewAction()
        {
            var agent = CreateQueueAgent(ActionQueueMode.QueueManual, InterruptPolicy.Immediate);

            var queue = Manager.GetBuffer<ActionQueueEntry>(agent);
            var progress = Manager.GetComponentData<ActionQueueProgress>(agent);
            ActionQueueUtility.Enqueue(ref queue, ref progress, actionId: 10, actionType: 40);
            ActionQueueUtility.Enqueue(ref queue, ref progress, actionId: 11, actionType: 41);
            Manager.SetComponentData(agent, progress);

            Manager.SetComponentData(agent, new AgentActionState
            {
                CurrentActionId = 10, CurrentActionType = 40, Phase = AgentActionPhase.Executing
            });

            // Simulate scoring picking a different action
            Manager.SetComponentData(agent, new ScoringResult
            {
                BestActionId = 99, BestScore = 0.9f,
                PreviousBestActionId = 10, ActionChanged = true
            });

            Tick();

            Assert.IsTrue(IsEnabled<QueueInterrupted>(agent), "QueueInterrupted should fire");
            queue = Manager.GetBuffer<ActionQueueEntry>(agent);
            Assert.AreEqual(0, queue.Length, "Queue should be cleared on immediate interrupt");
        }

        [Test]
        public void InterruptFinishQueue_DoesNotInterrupt()
        {
            var agent = CreateQueueAgent(ActionQueueMode.QueueManual, InterruptPolicy.FinishQueue);

            var queue = Manager.GetBuffer<ActionQueueEntry>(agent);
            var progress = Manager.GetComponentData<ActionQueueProgress>(agent);
            ActionQueueUtility.Enqueue(ref queue, ref progress, actionId: 10, actionType: 40);
            ActionQueueUtility.Enqueue(ref queue, ref progress, actionId: 11, actionType: 41);
            Manager.SetComponentData(agent, progress);

            Manager.SetComponentData(agent, new AgentActionState
            {
                CurrentActionId = 10, CurrentActionType = 40, Phase = AgentActionPhase.Executing
            });

            // Scoring wants to switch
            Manager.SetComponentData(agent, new ScoringResult
            {
                BestActionId = 99, BestScore = 0.9f,
                PreviousBestActionId = 10, ActionChanged = true
            });

            Tick();

            Assert.IsFalse(IsEnabled<QueueInterrupted>(agent), "Should NOT interrupt with FinishQueue");
            queue = Manager.GetBuffer<ActionQueueEntry>(agent);
            Assert.AreEqual(2, queue.Length, "Queue should remain intact");
        }

        // === AllowRescore Tests ===

        [Test]
        public void AllowRescore_False_SkipsScoring()
        {
            var agent = CreateQueueAgent(ActionQueueMode.QueueManual);
            Manager.SetComponentData(agent, new ActionQueueConfig
            {
                Mode = ActionQueueMode.QueueManual,
                InterruptPolicy = InterruptPolicy.FinishQueue,
                AllowRescore = false
            });

            // Add a consideration so scoring would normally run
            var cons = Manager.GetBuffer<ConsiderationElement>(agent);
            cons.Add(new ConsiderationElement
            {
                ActionId = 99, InputType = ScoringInputType.Constant,
                InputParam = 50, CurveType = ResponseCurveType.Linear, CurveA = 1f
            });

            Manager.SetComponentData(agent, new ScoringResult
            {
                BestActionId = -1, PreviousBestActionId = -1
            });

            Tick();

            var result = Manager.GetComponentData<ScoringResult>(agent);
            Assert.AreEqual(-1, result.BestActionId,
                "Scoring should be skipped when AllowRescore=false");
        }

        // === ActionQueueUtility Tests ===

        [Test]
        public void Utility_Enqueue_AddsEntry()
        {
            var e = CreateQueueAgent(ActionQueueMode.QueueManual);
            var queue = Manager.GetBuffer<ActionQueueEntry>(e);
            var progress = Manager.GetComponentData<ActionQueueProgress>(e);

            ActionQueueUtility.Enqueue(ref queue, ref progress,
                actionId: 10, actionType: 40, targetPosition: new float3(5, 0, 0));

            Assert.AreEqual(1, queue.Length);
            Assert.AreEqual(10, queue[0].ActionId);
            Assert.AreEqual(40, queue[0].ActionType);
            Assert.AreEqual(1, progress.TotalEntries);
        }

        [Test]
        public void Utility_ClearQueue_ResetsEverything()
        {
            var e = CreateQueueAgent(ActionQueueMode.QueueManual);
            var queue = Manager.GetBuffer<ActionQueueEntry>(e);
            var progress = Manager.GetComponentData<ActionQueueProgress>(e);

            ActionQueueUtility.Enqueue(ref queue, ref progress, 10, 40);
            ActionQueueUtility.Enqueue(ref queue, ref progress, 11, 41);
            progress.CurrentIndex = 1;

            ActionQueueUtility.ClearQueue(ref queue, ref progress);

            Assert.AreEqual(0, queue.Length);
            Assert.AreEqual(0, progress.CurrentIndex);
            Assert.AreEqual(0, progress.TotalEntries);
        }
    }
}
