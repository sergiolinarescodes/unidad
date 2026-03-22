using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    /// <summary>
    /// Tests for ActionEventClearSystem — verifies action and queue
    /// event tags are cleared each frame.
    /// </summary>
    [TestFixture]
    public class ActionEventClearSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();
            var handle = GetOrCreateSystem<ActionEventClearSystem>();
            _group = CreateSimGroup(handle);
        }

        Entity CreateActionEntity()
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>());

            Manager.SetComponentData(e, new AgentActionState { CurrentActionId = -1 });
            SetEnabled<ActionStarted>(e, false);
            SetEnabled<ActionCompleted>(e, false);
            SetEnabled<ActionInterrupted>(e, false);
            return e;
        }

        Entity CreateQueueEntity()
        {
            // AgentActionState is required because ActionEventClearSystem has
            // RequireForUpdate<AgentActionState> — needs at least one entity with it.
            var e = CreateEntity(
                ComponentType.ReadWrite<AgentActionState>(),
                ComponentType.ReadWrite<ActionQueueConfig>(),
                ComponentType.ReadWrite<QueueAdvanced>(),
                ComponentType.ReadWrite<QueueCompleted>(),
                ComponentType.ReadWrite<QueueInterrupted>(),
                ComponentType.ReadWrite<ActionStarted>(),
                ComponentType.ReadWrite<ActionCompleted>(),
                ComponentType.ReadWrite<ActionInterrupted>());

            Manager.SetComponentData(e, new AgentActionState { CurrentActionId = -1 });
            Manager.SetComponentData(e, ActionQueueConfig.Default);
            SetEnabled<QueueAdvanced>(e, false);
            SetEnabled<QueueCompleted>(e, false);
            SetEnabled<QueueInterrupted>(e, false);
            SetEnabled<ActionStarted>(e, false);
            SetEnabled<ActionCompleted>(e, false);
            SetEnabled<ActionInterrupted>(e, false);
            return e;
        }

        [Test]
        public void ActionEvents_ClearedAfterOneFrame()
        {
            var e = CreateActionEntity();
            SetEnabled<ActionStarted>(e, true);
            SetEnabled<ActionCompleted>(e, true);
            SetEnabled<ActionInterrupted>(e, true);

            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<ActionStarted>(e));
            Assert.IsFalse(IsEnabled<ActionCompleted>(e));
            Assert.IsFalse(IsEnabled<ActionInterrupted>(e));
        }

        [Test]
        public void QueueEvents_ClearedAfterOneFrame()
        {
            var e = CreateQueueEntity();
            SetEnabled<QueueAdvanced>(e, true);
            SetEnabled<QueueCompleted>(e, true);
            SetEnabled<QueueInterrupted>(e, true);

            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<QueueAdvanced>(e));
            Assert.IsFalse(IsEnabled<QueueCompleted>(e));
            Assert.IsFalse(IsEnabled<QueueInterrupted>(e));
        }

        [Test]
        public void DisabledEvents_StayDisabled()
        {
            var e = CreateActionEntity();
            // All start disabled

            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<ActionStarted>(e));
            Assert.IsFalse(IsEnabled<ActionCompleted>(e));
            Assert.IsFalse(IsEnabled<ActionInterrupted>(e));
        }
    }
}
