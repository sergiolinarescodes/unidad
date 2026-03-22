using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests for FeedbackEvaluationSystem and FeedbackEventClearSystem.
    /// Verifies completion record processing, per-action stats, periodic evaluation,
    /// underperforming detection, and event clearing.
    /// </summary>
    [TestFixture]
    public class FeedbackSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        const int ActionGather = 10;
        const int ActionRest = 11;

        public override void SetUp()
        {
            base.SetUp();

            var clearSystem = GetOrCreateSystem<FeedbackEventClearSystem>();
            var evalSystem = GetOrCreateSystem<FeedbackEvaluationSystem>();
            _group = CreateSimGroup(clearSystem, evalSystem);
        }

        Entity CreateFeedbackConfig(float evalInterval = 5f, float reward = 1f, float penalty = -0.5f)
        {
            var e = CreateEntity(ComponentType.ReadWrite<FeedbackConfig>());
            Manager.SetComponentData(e, new FeedbackConfig
            {
                EvaluationInterval = evalInterval,
                SatisfactionDecayRate = 0.9f,
                RewardPerActionComplete = reward,
                PenaltyPerActionFail = penalty,
                NeedSatisfactionWeight = 0.6f,
                UnderperformingThreshold = 0.3f,
                MinActionsForEvaluation = 5
            });
            return e;
        }

        Entity CreateFeedbackAgent()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentFeedback>(),
                ComponentType.ReadWrite<FeedbackEvaluated>(),
                ComponentType.ReadWrite<StrategyUnderperforming>());

            Manager.SetComponentData(e, new AgentFeedback());

            AddBuffer<ActionFeedbackElement>(e);
            AddBuffer<ActionCompletionRecord>(e);

            // Needs buffer for satisfaction calculation
            var needs = AddBuffer<NeedElement>(e);
            needs.Add(new NeedElement
            {
                ResourceId = 1, CurrentUrgency = NeedUrgency.Satisfied
            });

            SetEnabled<FeedbackEvaluated>(e, false);
            SetEnabled<StrategyUnderperforming>(e, false);

            return e;
        }

        void AddCompletionRecord(Entity agent, int actionId, bool successful)
        {
            var records = Manager.GetBuffer<ActionCompletionRecord>(agent);
            records.Add(new ActionCompletionRecord
            {
                ActionId = actionId,
                ActionType = 40,
                CompletedTime = 1.0,
                WasSuccessful = successful
            });
        }

        [Test]
        public void SuccessfulCompletion_IncrementsCounters()
        {
            CreateFeedbackConfig();
            var agent = CreateFeedbackAgent();

            AddCompletionRecord(agent, ActionGather, successful: true);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var feedback = Manager.GetComponentData<AgentFeedback>(agent);
            Assert.AreEqual(1, feedback.ActionsCompleted);
            Assert.AreEqual(0, feedback.ActionsFailed);
            Assert.AreEqual(1f, feedback.CumulativeScore, 0.01f);
        }

        [Test]
        public void FailedCompletion_IncrementsFailCounters()
        {
            CreateFeedbackConfig();
            var agent = CreateFeedbackAgent();

            AddCompletionRecord(agent, ActionGather, successful: false);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var feedback = Manager.GetComponentData<AgentFeedback>(agent);
            Assert.AreEqual(0, feedback.ActionsCompleted);
            Assert.AreEqual(1, feedback.ActionsFailed);
            Assert.AreEqual(-0.5f, feedback.CumulativeScore, 0.01f);
        }

        [Test]
        public void MultipleRecords_ProcessedCorrectly()
        {
            CreateFeedbackConfig();
            var agent = CreateFeedbackAgent();

            AddCompletionRecord(agent, ActionGather, successful: true);
            AddCompletionRecord(agent, ActionGather, successful: true);
            AddCompletionRecord(agent, ActionRest, successful: false);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var feedback = Manager.GetComponentData<AgentFeedback>(agent);
            Assert.AreEqual(2, feedback.ActionsCompleted);
            Assert.AreEqual(1, feedback.ActionsFailed);
            // 2 * 1.0 + 1 * (-0.5) = 1.5
            Assert.AreEqual(1.5f, feedback.CumulativeScore, 0.01f);
        }

        [Test]
        public void CompletionRecords_ClearedAfterProcessing()
        {
            CreateFeedbackConfig();
            var agent = CreateFeedbackAgent();

            AddCompletionRecord(agent, ActionGather, successful: true);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var records = Manager.GetBuffer<ActionCompletionRecord>(agent);
            Assert.AreEqual(0, records.Length, "Completion records should be cleared after processing");
        }

        [Test]
        public void PerActionFeedback_TrackedSeparately()
        {
            CreateFeedbackConfig();
            var agent = CreateFeedbackAgent();

            AddCompletionRecord(agent, ActionGather, successful: true);
            AddCompletionRecord(agent, ActionGather, successful: true);
            AddCompletionRecord(agent, ActionRest, successful: false);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var actionFeedbacks = Manager.GetBuffer<ActionFeedbackElement>(agent);
            Assert.AreEqual(2, actionFeedbacks.Length, "Should have 2 action feedback entries");

            // Find Gather feedback
            int gatherIdx = -1, restIdx = -1;
            for (int i = 0; i < actionFeedbacks.Length; i++)
            {
                if (actionFeedbacks[i].ActionId == ActionGather) gatherIdx = i;
                if (actionFeedbacks[i].ActionId == ActionRest) restIdx = i;
            }

            Assert.IsTrue(gatherIdx >= 0, "Should have Gather feedback");
            Assert.AreEqual(2, actionFeedbacks[gatherIdx].CompletionCount);
            Assert.AreEqual(0, actionFeedbacks[gatherIdx].FailureCount);
            Assert.AreEqual(1f, actionFeedbacks[gatherIdx].AverageReward, 0.01f);

            Assert.IsTrue(restIdx >= 0, "Should have Rest feedback");
            Assert.AreEqual(0, actionFeedbacks[restIdx].CompletionCount);
            Assert.AreEqual(1, actionFeedbacks[restIdx].FailureCount);
            Assert.AreEqual(-0.5f, actionFeedbacks[restIdx].AverageReward, 0.01f);
        }

        [Test]
        public void PeriodicEvaluation_FiresAfterInterval()
        {
            CreateFeedbackConfig(evalInterval: 2f);
            var agent = CreateFeedbackAgent();

            // Frame at t=1: not enough time for evaluation (interval=2)
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);
            Assert.IsFalse(IsEnabled<FeedbackEvaluated>(agent),
                "Should not evaluate before interval elapsed");

            // Frame at t=3: enough time for evaluation (3 - 0 >= 2)
            SetWorldTime(3.0, 2.0f);
            UpdateGroup(_group);
            Assert.IsTrue(IsEnabled<FeedbackEvaluated>(agent),
                "Should evaluate after interval elapsed");

            var feedback = Manager.GetComponentData<AgentFeedback>(agent);
            Assert.AreEqual(3.0, feedback.LastEvaluationTime, 0.01);
        }

        [Test]
        public void FeedbackEvents_ClearedNextFrame()
        {
            CreateFeedbackConfig(evalInterval: 0.1f);
            var agent = CreateFeedbackAgent();

            // Trigger evaluation
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);
            Assert.IsTrue(IsEnabled<FeedbackEvaluated>(agent));

            // Next frame: should be cleared
            SetWorldTime(1.1, 0.1f);
            UpdateGroup(_group);
            Assert.IsFalse(IsEnabled<FeedbackEvaluated>(agent),
                "FeedbackEvaluated should be cleared next frame");
        }

        [Test]
        public void Underperforming_FiresWhenPerformanceLow()
        {
            CreateFeedbackConfig(evalInterval: 0.1f, reward: 1f, penalty: -0.5f);
            var agent = CreateFeedbackAgent();

            // Set needs to Critical urgency (bad satisfaction)
            var needs = Manager.GetBuffer<NeedElement>(agent);
            var n = needs[0];
            n.CurrentUrgency = NeedUrgency.Critical;
            needs[0] = n;

            // Add many failed actions so totalActions > 5 and successRate is low
            for (int i = 0; i < 6; i++)
                AddCompletionRecord(agent, ActionGather, successful: false);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<StrategyUnderperforming>(agent),
                "Should fire underperforming when satisfaction low and high failure rate");
        }

        [Test]
        public void NotUnderperforming_WhenTooFewActions()
        {
            CreateFeedbackConfig(evalInterval: 0.1f);
            var agent = CreateFeedbackAgent();

            // Set needs to Critical but only 2 actions (below threshold of 5)
            var needs = Manager.GetBuffer<NeedElement>(agent);
            var n = needs[0];
            n.CurrentUrgency = NeedUrgency.Critical;
            needs[0] = n;

            AddCompletionRecord(agent, ActionGather, successful: false);
            AddCompletionRecord(agent, ActionGather, successful: false);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<StrategyUnderperforming>(agent),
                "Should not fire underperforming with < 5 total actions");
        }

        [Test]
        public void NeedSatisfaction_HighUrgencyLowersSatisfaction()
        {
            CreateFeedbackConfig(evalInterval: 0.1f);
            var agent = CreateFeedbackAgent();

            // Set needs to Critical
            var needs = Manager.GetBuffer<NeedElement>(agent);
            var n = needs[0];
            n.CurrentUrgency = NeedUrgency.Critical;
            needs[0] = n;

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var feedback = Manager.GetComponentData<AgentFeedback>(agent);
            // Critical = 3, so satisfaction = 1 - (3/3) = 0
            // AverageNeedSatisfaction = 0 * 0.9 + 0 * 0.1 = 0
            Assert.AreEqual(0f, feedback.AverageNeedSatisfaction, 0.01f,
                "Critical urgency should give 0 satisfaction");
        }

        [Test]
        public void NeedSatisfaction_SatisfiedUrgencyIsHigh()
        {
            CreateFeedbackConfig(evalInterval: 0.1f);
            var agent = CreateFeedbackAgent();

            // Needs default to Satisfied
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var feedback = Manager.GetComponentData<AgentFeedback>(agent);
            // Satisfied = 0, so satisfaction = 1 - (0/3) = 1
            // AverageNeedSatisfaction = 0 * 0.9 + 1 * 0.1 = 0.1
            Assert.AreEqual(0.1f, feedback.AverageNeedSatisfaction, 0.05f,
                "Satisfied urgency should give high satisfaction (decayed from 0)");
        }

        [Test]
        public void FeedbackUtility_GetActionSuccessRate()
        {
            CreateFeedbackConfig();
            var agent = CreateFeedbackAgent();

            AddCompletionRecord(agent, ActionGather, successful: true);
            AddCompletionRecord(agent, ActionGather, successful: true);
            AddCompletionRecord(agent, ActionGather, successful: false);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var feedbacks = Manager.GetBuffer<ActionFeedbackElement>(agent);
            float rate = FeedbackUtility.GetActionSuccessRate(in feedbacks, ActionGather);
            // 2 successes / 3 total = 0.667
            Assert.AreEqual(0.667f, rate, 0.01f);
        }

        [Test]
        public void FeedbackUtility_GetOverallSuccessRate()
        {
            CreateFeedbackConfig();
            var agent = CreateFeedbackAgent();

            AddCompletionRecord(agent, ActionGather, successful: true);
            AddCompletionRecord(agent, ActionRest, successful: false);
            AddCompletionRecord(agent, ActionGather, successful: true);
            AddCompletionRecord(agent, ActionRest, successful: true);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var feedback = Manager.GetComponentData<AgentFeedback>(agent);
            float rate = FeedbackUtility.GetOverallSuccessRate(in feedback);
            // 3 successes / 4 total = 0.75
            Assert.AreEqual(0.75f, rate, 0.01f);
        }

        [Test]
        public void FeedbackUtility_ResetFeedback()
        {
            CreateFeedbackConfig();
            var agent = CreateFeedbackAgent();

            AddCompletionRecord(agent, ActionGather, successful: true);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var feedback = Manager.GetComponentData<AgentFeedback>(agent);
            var actionFeedbacks = Manager.GetBuffer<ActionFeedbackElement>(agent);

            FeedbackUtility.ResetFeedback(ref feedback, ref actionFeedbacks);
            Manager.SetComponentData(agent, feedback);

            Assert.AreEqual(0f, feedback.CumulativeScore, 0.01f);
            Assert.AreEqual(0, feedback.ActionsCompleted);
            Assert.AreEqual(0, feedback.ActionsFailed);
            Assert.AreEqual(0.5f, feedback.AverageNeedSatisfaction, 0.01f);
            Assert.AreEqual(0, actionFeedbacks.Length);
        }

        [Test]
        public void NoRecords_NoCounterChanges()
        {
            CreateFeedbackConfig();
            var agent = CreateFeedbackAgent();

            // No completion records added
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var feedback = Manager.GetComponentData<AgentFeedback>(agent);
            Assert.AreEqual(0, feedback.ActionsCompleted);
            Assert.AreEqual(0, feedback.ActionsFailed);
            Assert.AreEqual(0f, feedback.CumulativeScore, 0.01f);
        }
    }
}
