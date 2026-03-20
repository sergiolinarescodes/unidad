using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class TimerSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();
            var handle = GetOrCreateSystem<TimerSystem>();
            _group = CreateSimGroup(handle);
        }

        Entity CreateTimer(float duration, bool loop = false, bool paused = false)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<TimerData>(),
                ComponentType.ReadWrite<TimerCompleted>());
            Manager.SetComponentData(e, new TimerData
            {
                Duration = duration,
                Elapsed = 0f,
                Paused = paused,
                Loop = loop
            });
            SetEnabled<TimerCompleted>(e, false);
            return e;
        }

        [Test]
        public void Tick_AccumulatesElapsed()
        {
            var e = CreateTimer(2f);
            SetWorldTime(0.5, 0.5f);
            UpdateGroup(_group);

            var timer = Manager.GetComponentData<TimerData>(e);
            Assert.AreEqual(0.5f, timer.Elapsed, 0.001f);
            Assert.IsFalse(IsEnabled<TimerCompleted>(e));
        }

        [Test]
        public void Tick_CompletesAtDuration()
        {
            var e = CreateTimer(1f);
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<TimerCompleted>(e));
        }

        [Test]
        public void Tick_CompletesWhenExceedingDuration()
        {
            var e = CreateTimer(1f);
            SetWorldTime(1.5, 1.5f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<TimerCompleted>(e));
        }

        [Test]
        public void NonLooping_PausesAfterCompletion()
        {
            var e = CreateTimer(1f, loop: false);
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            var timer = Manager.GetComponentData<TimerData>(e);
            Assert.IsTrue(timer.Paused);
        }

        [Test]
        public void Looping_ResetsElapsed()
        {
            var e = CreateTimer(1f, loop: true);
            SetWorldTime(1.2, 1.2f);
            UpdateGroup(_group);

            var timer = Manager.GetComponentData<TimerData>(e);
            Assert.AreEqual(0.2f, timer.Elapsed, 0.01f); // 1.2 - 1.0
            Assert.IsFalse(timer.Paused);
            Assert.IsTrue(IsEnabled<TimerCompleted>(e));
        }

        [Test]
        public void Paused_NoAccumulation()
        {
            var e = CreateTimer(1f, paused: true);
            SetWorldTime(0.5, 0.5f);
            UpdateGroup(_group);

            var timer = Manager.GetComponentData<TimerData>(e);
            Assert.AreEqual(0f, timer.Elapsed, 0.001f);
        }

        [Test]
        public void CompletedFlag_ClearedNextFrame()
        {
            var e = CreateTimer(1f);
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);
            Assert.IsTrue(IsEnabled<TimerCompleted>(e));

            // Next frame: timer is paused, flag should be cleared
            SetWorldTime(2.0, 1.0f);
            UpdateGroup(_group);
            Assert.IsFalse(IsEnabled<TimerCompleted>(e));
        }

        [Test]
        public void Cancelled_EntityDestroyed()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<TimerData>(),
                ComponentType.ReadWrite<TimerCompleted>(),
                ComponentType.ReadWrite<TimerCancelled>());
            Manager.SetComponentData(e, new TimerData { Duration = 10f });
            SetEnabled<TimerCompleted>(e, false);
            // TimerCancelled starts enabled by default — triggers destroy

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsFalse(Manager.Exists(e));
        }

        [Test]
        public void MultipleTimers_IndependentTracking()
        {
            var a = CreateTimer(1f);
            var b = CreateTimer(2f);

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<TimerCompleted>(a));
            Assert.IsFalse(IsEnabled<TimerCompleted>(b));

            var timerB = Manager.GetComponentData<TimerData>(b);
            Assert.AreEqual(1.0f, timerB.Elapsed, 0.001f);
        }

        [Test]
        public void ZeroDuration_CompletesImmediately()
        {
            var e = CreateTimer(0f);
            SetWorldTime(0.016, 0.016f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<TimerCompleted>(e));
        }

        [Test]
        public void Looping_FiresCompletedEachLoop()
        {
            var e = CreateTimer(1f, loop: true);

            // First loop
            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);
            Assert.IsTrue(IsEnabled<TimerCompleted>(e));

            // Second loop
            SetWorldTime(2.0, 1.0f);
            UpdateGroup(_group);
            Assert.IsTrue(IsEnabled<TimerCompleted>(e));
        }
    }
}
