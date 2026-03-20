using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class StateMachineSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();
            var handle = GetOrCreateSystem<StateMachineSystem>();
            _group = CreateSimGroup(handle);
        }

        Entity CreateStateMachine(int initialState)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<StateMachineData>(),
                ComponentType.ReadWrite<StateEntered>(),
                ComponentType.ReadWrite<StateExited>());
            Manager.SetComponentData(e, new StateMachineData
            {
                CurrentState = initialState,
                PreviousState = 0,
                TransitionRequested = false,
                RequestedState = 0
            });
            SetEnabled<StateEntered>(e, false);
            SetEnabled<StateExited>(e, false);
            return e;
        }

        void RequestTransition(Entity e, int targetState)
        {
            var sm = Manager.GetComponentData<StateMachineData>(e);
            sm.TransitionRequested = true;
            sm.RequestedState = targetState;
            Manager.SetComponentData(e, sm);
        }

        [Test]
        public void NoTransition_ClearsPreviousFlags_NoNewFlags()
        {
            var e = CreateStateMachine(1);
            SetEnabled<StateEntered>(e, true);
            SetEnabled<StateExited>(e, true);

            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<StateEntered>(e));
            Assert.IsFalse(IsEnabled<StateExited>(e));
        }

        [Test]
        public void Transition_SetsCurrentAndPreviousState()
        {
            var e = CreateStateMachine(1);
            RequestTransition(e, 2);

            UpdateGroup(_group);

            var sm = Manager.GetComponentData<StateMachineData>(e);
            Assert.AreEqual(2, sm.CurrentState);
            Assert.AreEqual(1, sm.PreviousState);
        }

        [Test]
        public void Transition_ClearsTransitionRequested()
        {
            var e = CreateStateMachine(1);
            RequestTransition(e, 2);

            UpdateGroup(_group);

            var sm = Manager.GetComponentData<StateMachineData>(e);
            Assert.IsFalse(sm.TransitionRequested);
        }

        [Test]
        public void Transition_SetsEnteredAndExitedFlags()
        {
            var e = CreateStateMachine(1);
            RequestTransition(e, 2);

            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<StateEntered>(e));
            Assert.IsTrue(IsEnabled<StateExited>(e));
        }

        [Test]
        public void Flags_ClearedNextFrame()
        {
            var e = CreateStateMachine(1);
            RequestTransition(e, 2);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<StateEntered>(e));

            // Next frame: no transition requested
            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<StateEntered>(e));
            Assert.IsFalse(IsEnabled<StateExited>(e));
        }

        [Test]
        public void MultipleEntities_IndependentTransitions()
        {
            var a = CreateStateMachine(1);
            var b = CreateStateMachine(10);
            RequestTransition(a, 2);
            // b has no transition

            UpdateGroup(_group);

            Assert.AreEqual(2, Manager.GetComponentData<StateMachineData>(a).CurrentState);
            Assert.AreEqual(10, Manager.GetComponentData<StateMachineData>(b).CurrentState);
            Assert.IsTrue(IsEnabled<StateEntered>(a));
            Assert.IsFalse(IsEnabled<StateEntered>(b));
        }

        [Test]
        public void TransitionToSameState_StillFiresEvents()
        {
            var e = CreateStateMachine(1);
            RequestTransition(e, 1);

            UpdateGroup(_group);

            var sm = Manager.GetComponentData<StateMachineData>(e);
            Assert.AreEqual(1, sm.CurrentState);
            Assert.AreEqual(1, sm.PreviousState);
            Assert.IsTrue(IsEnabled<StateEntered>(e));
            Assert.IsTrue(IsEnabled<StateExited>(e));
        }

        [Test]
        public void ConsecutiveTransitions_TrackPreviousState()
        {
            var e = CreateStateMachine(1);
            RequestTransition(e, 2);
            UpdateGroup(_group);

            RequestTransition(e, 3);
            UpdateGroup(_group);

            var sm = Manager.GetComponentData<StateMachineData>(e);
            Assert.AreEqual(3, sm.CurrentState);
            Assert.AreEqual(2, sm.PreviousState);
        }

        [Test]
        public void NoTransition_StateUnchanged()
        {
            var e = CreateStateMachine(5);
            UpdateGroup(_group);

            var sm = Manager.GetComponentData<StateMachineData>(e);
            Assert.AreEqual(5, sm.CurrentState);
        }
    }
}
