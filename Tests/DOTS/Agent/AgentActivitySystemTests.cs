using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class AgentActivitySystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();
            var handle = GetOrCreateSystem<AgentActivitySystem>();
            _group = CreateSimGroup(handle);
        }

        Entity CreateActivityAgent(AgentActionPhase phase, int actionId = -1)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentActivity>(),
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<AgentTarget>(),
                ComponentType.ReadWrite<ActivityChanged>(),
                ComponentType.ReadWrite<ActionQueueProgress>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>());

            Manager.SetComponentData(e, new AgentActionState
            {
                Phase = phase,
                CurrentActionId = actionId,
                CurrentActionType = actionId >= 0 ? 40 : 0
            });
            Manager.SetComponentData(e, new AgentActivity());
            Manager.SetComponentData(e, LocalTransform.FromPosition(float3.zero));
            SetEnabled<ActivityChanged>(e, false);

            return e;
        }

        [Test]
        public void IdlePhase_SetsActivityIdle()
        {
            var agent = CreateActivityAgent(AgentActionPhase.None);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var activity = Manager.GetComponentData<AgentActivity>(agent);
            Assert.AreEqual(AgentActivityType.Idle, activity.CurrentActivity);
        }

        [Test]
        public void NavigatingPhase_SetsActivityMoving()
        {
            var agent = CreateActivityAgent(AgentActionPhase.Navigating, actionId: 10);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var activity = Manager.GetComponentData<AgentActivity>(agent);
            Assert.AreEqual(AgentActivityType.Moving, activity.CurrentActivity);
            Assert.AreEqual(10, activity.CurrentActionId);
        }

        [Test]
        public void ExecutingPhase_SetsActivityPerforming()
        {
            var agent = CreateActivityAgent(AgentActionPhase.Executing, actionId: 10);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var activity = Manager.GetComponentData<AgentActivity>(agent);
            Assert.AreEqual(AgentActivityType.PerformingAction, activity.CurrentActivity);
        }

        [Test]
        public void StartingPhase_SetsActivityQueued()
        {
            var agent = CreateActivityAgent(AgentActionPhase.Starting, actionId: 10);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var activity = Manager.GetComponentData<AgentActivity>(agent);
            Assert.AreEqual(AgentActivityType.Queued, activity.CurrentActivity);
        }

        [Test]
        public void ActivityChanged_FiresOnTransition()
        {
            var agent = CreateActivityAgent(AgentActionPhase.None);

            // First frame: Idle (from default Idle → Idle, no change)
            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);
            Assert.IsFalse(IsEnabled<ActivityChanged>(agent), "No change on first idle frame");

            // Switch to Navigating
            Manager.SetComponentData(agent, new AgentActionState
            {
                Phase = AgentActionPhase.Navigating, CurrentActionId = 10, CurrentActionType = 40
            });

            SetWorldTime(0.2, 0.1f);
            UpdateGroup(_group);
            Assert.IsTrue(IsEnabled<ActivityChanged>(agent), "Should fire on Idle→Moving");
        }

        [Test]
        public void InterruptedPhase_ResetsToIdle()
        {
            var agent = CreateActivityAgent(AgentActionPhase.Interrupted, actionId: 10);

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var activity = Manager.GetComponentData<AgentActivity>(agent);
            Assert.AreEqual(AgentActivityType.Idle, activity.CurrentActivity);
            Assert.AreEqual(-1, activity.CurrentActionId, "Interrupted should clear action ID");
        }

        [Test]
        public void QueuedEntries_ShowsQueued_WhenPhaseNone()
        {
            var agent = CreateActivityAgent(AgentActionPhase.None);
            Manager.SetComponentData(agent, new ActionQueueProgress
            {
                CurrentIndex = 0, TotalEntries = 3
            });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var activity = Manager.GetComponentData<AgentActivity>(agent);
            Assert.AreEqual(AgentActivityType.Queued, activity.CurrentActivity);
        }
    }
}
