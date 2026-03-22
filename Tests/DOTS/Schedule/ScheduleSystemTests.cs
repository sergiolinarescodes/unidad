using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class ScheduleSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;
        Entity _worldTimeEntity;

        public override void SetUp()
        {
            base.SetUp();

            var schedClear = GetOrCreateSystem<ScheduleEventClearSystem>();
            var worldTime = GetOrCreateSystem<WorldTimeSystem>();
            var schedule = GetOrCreateSystem<ScheduleSystem>();
            _group = CreateSimGroup(schedClear, worldTime, schedule);

            // Create WorldTimeData singleton
            _worldTimeEntity = CreateEntity(ComponentType.ReadWrite<WorldTimeData>());
            Manager.SetComponentData(_worldTimeEntity, new WorldTimeData
            {
                TimeOfDay = 8f,
                DayLength = 24f, // 1 second = 1 hour for easy testing
                CurrentDay = 1,
                TimeScale = 1f
            });
        }

        Entity CreateScheduleDef(params ScheduleSlotElement[] slots)
        {
            var e = CreateEntity(ComponentType.ReadWrite<ScheduleDefinition>());
            Manager.SetComponentData(e, new ScheduleDefinition { ScheduleId = 1 });

            var buf = AddBuffer<ScheduleSlotElement>(e);
            foreach (var s in slots) buf.Add(s);

            return e;
        }

        Entity CreateScheduledAgent(int scheduleId)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<ScheduleData>(),
                ComponentType.ReadWrite<ScheduleSlotChanged>(),
                ComponentType.ReadWrite<StateMachineData>(),
                ComponentType.ReadWrite<StateEntered>(),
                ComponentType.ReadWrite<StateExited>(),
                ComponentType.ReadWrite<StrategyAssignRequest>(),
                ComponentType.ReadWrite<StrategyAssigned>());

            Manager.SetComponentData(e, new ScheduleData { ScheduleId = scheduleId, CurrentSlotIndex = -1 });
            SetEnabled<ScheduleSlotChanged>(e, false);
            SetEnabled<StrategyAssignRequest>(e, false);
            SetEnabled<StrategyAssigned>(e, false);
            SetEnabled<StateEntered>(e, false);
            SetEnabled<StateExited>(e, false);

            return e;
        }

        void SetTimeOfDay(float hours)
        {
            var t = Manager.GetComponentData<WorldTimeData>(_worldTimeEntity);
            t.TimeOfDay = hours;
            Manager.SetComponentData(_worldTimeEntity, t);
        }

        [Test]
        public void WorldTime_TicksCorrectly()
        {
            // DayLength=24s, TimeScale=1. After 1s dt, time advances 1 hour.
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var t = Manager.GetComponentData<WorldTimeData>(_worldTimeEntity);
            Assert.AreEqual(9f, t.TimeOfDay, 0.1f, "8 + 1 hour = 9");
        }

        [Test]
        public void WorldTime_WrapsAt24()
        {
            SetTimeOfDay(23.5f);
            SetWorldTime(1.0, 1.0f); // +1 hour → 24.5 → wraps to 0.5
            UpdateGroup(_group);

            var t = Manager.GetComponentData<WorldTimeData>(_worldTimeEntity);
            Assert.AreEqual(0.5f, t.TimeOfDay, 0.1f);
            Assert.AreEqual(2, t.CurrentDay, "Day should increment on wrap");
        }

        [Test]
        public void SlotDetected_NormalSlot()
        {
            CreateScheduleDef(
                new ScheduleSlotElement { StartTime = 6f, EndTime = 12f, RequiredStateId = 1, StrategyOverrideId = -1, PriorityActionId = -1 },
                new ScheduleSlotElement { StartTime = 12f, EndTime = 18f, RequiredStateId = 2, StrategyOverrideId = -1, PriorityActionId = -1 });

            var agent = CreateScheduledAgent(1);
            SetTimeOfDay(10f); // Should match slot 0 (6..12)

            SetWorldTime(0.01, 0.01f);
            UpdateGroup(_group);

            var sched = Manager.GetComponentData<ScheduleData>(agent);
            Assert.AreEqual(0, sched.CurrentSlotIndex, "Should detect slot 0 (6..12)");
            Assert.IsTrue(IsEnabled<ScheduleSlotChanged>(agent));
        }

        [Test]
        public void OvernightSlot_Detected()
        {
            CreateScheduleDef(
                new ScheduleSlotElement { StartTime = 22f, EndTime = 6f, RequiredStateId = 3, StrategyOverrideId = -1, PriorityActionId = -1 });

            var agent = CreateScheduledAgent(1);
            SetTimeOfDay(23f); // Should match overnight slot (22..6)

            SetWorldTime(0.01, 0.01f);
            UpdateGroup(_group);

            var sched = Manager.GetComponentData<ScheduleData>(agent);
            Assert.AreEqual(0, sched.CurrentSlotIndex);
        }

        [Test]
        public void OvernightSlot_DetectedAfterMidnight()
        {
            CreateScheduleDef(
                new ScheduleSlotElement { StartTime = 22f, EndTime = 6f, RequiredStateId = 3, StrategyOverrideId = -1, PriorityActionId = -1 });

            var agent = CreateScheduledAgent(1);
            SetTimeOfDay(3f); // 3am — inside overnight slot (22..6)

            SetWorldTime(0.01, 0.01f);
            UpdateGroup(_group);

            var sched = Manager.GetComponentData<ScheduleData>(agent);
            Assert.AreEqual(0, sched.CurrentSlotIndex);
        }

        [Test]
        public void SlotChange_TriggersStateTransition()
        {
            CreateScheduleDef(
                new ScheduleSlotElement { StartTime = 8f, EndTime = 17f, RequiredStateId = 5, StrategyOverrideId = -1, PriorityActionId = -1 });

            var agent = CreateScheduledAgent(1);
            SetTimeOfDay(10f);

            SetWorldTime(0.01, 0.01f);
            UpdateGroup(_group);

            var sm = Manager.GetComponentData<StateMachineData>(agent);
            Assert.IsTrue(sm.TransitionRequested, "Should request state transition");
            Assert.AreEqual(5, sm.RequestedState);
        }

        [Test]
        public void SlotChange_TriggersStrategyOverride()
        {
            CreateScheduleDef(
                new ScheduleSlotElement { StartTime = 8f, EndTime = 17f, RequiredStateId = -1, StrategyOverrideId = 42, PriorityActionId = -1 });

            var agent = CreateScheduledAgent(1);
            SetTimeOfDay(10f);

            SetWorldTime(0.01, 0.01f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<StrategyAssignRequest>(agent), "Should trigger strategy assign");
            var req = Manager.GetComponentData<StrategyAssignRequest>(agent);
            Assert.AreEqual(42, req.StrategyId);
        }

        [Test]
        public void NoChange_WhenSameSlot()
        {
            CreateScheduleDef(
                new ScheduleSlotElement { StartTime = 8f, EndTime = 17f, RequiredStateId = 1, StrategyOverrideId = -1, PriorityActionId = -1 });

            var agent = CreateScheduledAgent(1);
            SetTimeOfDay(10f);

            // First tick: detects slot 0
            SetWorldTime(0.01, 0.01f);
            UpdateGroup(_group);
            Assert.IsTrue(IsEnabled<ScheduleSlotChanged>(agent));

            // Clear event
            SetWorldTime(0.02, 0.01f);
            UpdateGroup(_group); // ScheduleEventClearSystem clears it

            // Move time slightly but stay in same slot
            SetTimeOfDay(11f);
            SetWorldTime(0.03, 0.01f);
            UpdateGroup(_group);

            // ScheduleSlotChanged should have been cleared and NOT re-fired
            Assert.IsFalse(IsEnabled<ScheduleSlotChanged>(agent),
                "Same slot — no event should fire");
        }
    }
}
