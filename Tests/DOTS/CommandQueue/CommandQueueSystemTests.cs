using NUnit.Framework;
using Unity.Entities;

namespace Unidad.Core.DOTS.Tests
{
    [TestFixture]
    public class CommandQueueSystemTests : DOTSTestFixture
    {
        SimulationSystemGroup _group;

        public override void SetUp()
        {
            base.SetUp();
            var handle = GetOrCreateSystem<CommandQueueSystem>();
            _group = CreateSimGroup(handle);
        }

        Entity CreateCommandQueue(params CommandEntry[] commands)
        {
            var e = CreateEntity(
                ComponentType.ReadWrite<CommandQueueData>(),
                ComponentType.ReadWrite<CommandEntry>(),
                ComponentType.ReadWrite<CommandCompleted>(),
                ComponentType.ReadWrite<CommandFailed>(),
                ComponentType.ReadWrite<QueueEmpty>());
            Manager.SetComponentData(e, new CommandQueueData { IsPaused = false, CurrentIndex = 0 });
            SetEnabled<CommandCompleted>(e, false);
            SetEnabled<CommandFailed>(e, false);
            SetEnabled<QueueEmpty>(e, false);
            var buffer = Manager.GetBuffer<CommandEntry>(e);
            foreach (var cmd in commands)
                buffer.Add(cmd);
            return e;
        }

        [Test]
        public void EmptyQueue_SetsQueueEmpty()
        {
            var e = CreateCommandQueue(); // no commands

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<QueueEmpty>(e));
        }

        [Test]
        public void None_CompletesImmediately()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Pending });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<CommandCompleted>(e));
            var queue = Manager.GetComponentData<CommandQueueData>(e);
            Assert.AreEqual(1, queue.CurrentIndex);
        }

        [Test]
        public void None_SetsCompletedFlag()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Pending });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<CommandCompleted>(e));
        }

        [Test]
        public void Wait_AccumulatesElapsed()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.Wait, Duration = 2f, Status = CommandStatus.Pending });

            SetWorldTime(0.5, 0.5f);
            UpdateGroup(_group);

            var commands = Manager.GetBuffer<CommandEntry>(e);
            Assert.AreEqual(CommandStatus.Running, commands[0].Status);
            Assert.AreEqual(0.5f, commands[0].Elapsed, 0.01f);
            Assert.IsFalse(IsEnabled<CommandCompleted>(e));
        }

        [Test]
        public void Wait_CompletesAtDuration()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.Wait, Duration = 1f, Status = CommandStatus.Pending });

            SetWorldTime(1.0, 1.0f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<CommandCompleted>(e));
        }

        [Test]
        public void Wait_DoesNotCompleteEarly()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.Wait, Duration = 2f, Status = CommandStatus.Pending });

            SetWorldTime(0.5, 0.5f);
            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<CommandCompleted>(e));
        }

        [Test]
        public void Sequential_OnePerFrame()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Pending },
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Pending });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var queue = Manager.GetComponentData<CommandQueueData>(e);
            Assert.AreEqual(1, queue.CurrentIndex); // first completed, not second

            SetWorldTime(0.2, 0.1f);
            UpdateGroup(_group);

            queue = Manager.GetComponentData<CommandQueueData>(e);
            Assert.AreEqual(2, queue.CurrentIndex); // second now completed
        }

        [Test]
        public void Paused_NoProcessing()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Pending });
            Manager.SetComponentData(e, new CommandQueueData { IsPaused = true, CurrentIndex = 0 });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsFalse(IsEnabled<CommandCompleted>(e));
            var queue = Manager.GetComponentData<CommandQueueData>(e);
            Assert.AreEqual(0, queue.CurrentIndex);
        }

        [Test]
        public void QueueEmpty_OnLastCommandCompletion()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Pending });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<QueueEmpty>(e));
        }

        [Test]
        public void Failed_SetsFailedFlag()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Failed });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<CommandFailed>(e));
            var queue = Manager.GetComponentData<CommandQueueData>(e);
            Assert.AreEqual(1, queue.CurrentIndex);
        }

        [Test]
        public void Flags_ClearedFromPreviousFrame()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Pending },
                new CommandEntry { Type = CommandType.Wait, Duration = 10f, Status = CommandStatus.Pending });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);
            Assert.IsTrue(IsEnabled<CommandCompleted>(e));

            // Next frame: second command is Wait, not yet complete
            SetWorldTime(0.2, 0.1f);
            UpdateGroup(_group);
            Assert.IsFalse(IsEnabled<CommandCompleted>(e)); // cleared
        }

        [Test]
        public void Pending_TransitionsToRunning()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.Wait, Duration = 5f, Status = CommandStatus.Pending });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var commands = Manager.GetBuffer<CommandEntry>(e);
            Assert.AreEqual(CommandStatus.Running, commands[0].Status);
        }

        [Test]
        public void Failed_AdvancesIndex()
        {
            var e = CreateCommandQueue(
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Failed },
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Pending });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            var queue = Manager.GetComponentData<CommandQueueData>(e);
            Assert.AreEqual(1, queue.CurrentIndex); // advanced past failed
        }

        [Test]
        public void MultipleEntities_IndependentProcessing()
        {
            var a = CreateCommandQueue(
                new CommandEntry { Type = CommandType.None, Status = CommandStatus.Pending });
            var b = CreateCommandQueue(
                new CommandEntry { Type = CommandType.Wait, Duration = 5f, Status = CommandStatus.Pending });

            SetWorldTime(0.1, 0.1f);
            UpdateGroup(_group);

            Assert.IsTrue(IsEnabled<CommandCompleted>(a));
            Assert.IsFalse(IsEnabled<CommandCompleted>(b));
        }
    }
}
