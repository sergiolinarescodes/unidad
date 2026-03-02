using System.Collections.Generic;
using NUnit.Framework;
using Unidad.Core.Patterns.CommandQueue;

namespace Unidad.Core.Tests.Patterns
{
    [TestFixture]
    public class CommandQueueTests
    {
        private sealed class TestContext : ICommandContext { }

        private sealed class InstantCommand : ICommand
        {
            public string Id { get; }
            public bool WasCancelled { get; private set; }

            public InstantCommand(string id) => Id = id;

            public CommandStatus Execute(ICommandContext context, float deltaTime)
                => CommandStatus.Completed;

            public void Cancel() => WasCancelled = true;
        }

        private sealed class FailingCommand : ICommand
        {
            public string Id { get; }

            public FailingCommand(string id) => Id = id;

            public CommandStatus Execute(ICommandContext context, float deltaTime)
                => CommandStatus.Failed;

            public void Cancel() { }
        }

        private sealed class DurationCommand : ICommand
        {
            public string Id { get; }
            private readonly float _duration;
            private float _elapsed;

            public DurationCommand(string id, float duration)
            {
                Id = id;
                _duration = duration;
            }

            public CommandStatus Execute(ICommandContext context, float deltaTime)
            {
                _elapsed += deltaTime;
                return _elapsed >= _duration ? CommandStatus.Completed : CommandStatus.Running;
            }

            public void Cancel() { }
        }

        private TestContext _context;
        private CommandQueue _queue;

        [SetUp]
        public void SetUp()
        {
            _context = new TestContext();
            _queue = new CommandQueue();
        }

        [Test]
        public void IsEmpty_WhenNewQueue_ReturnsTrue()
        {
            Assert.That(_queue.IsEmpty, Is.True);
            Assert.That(_queue.Count, Is.EqualTo(0));
        }

        [Test]
        public void Enqueue_IncreasesCount()
        {
            _queue.Enqueue(new InstantCommand("a"));
            Assert.That(_queue.Count, Is.EqualTo(1));
            Assert.That(_queue.IsEmpty, Is.False);
        }

        [Test]
        public void Tick_ExecutesAndCompletesInstantCommand()
        {
            var completed = new List<string>();
            _queue.OnCommandCompleted += cmd => completed.Add(cmd.Id);

            _queue.Enqueue(new InstantCommand("a"));
            _queue.Tick(_context, 0.016f);

            Assert.That(completed, Is.EqualTo(new[] { "a" }));
            Assert.That(_queue.IsEmpty, Is.True);
        }

        [Test]
        public void Tick_ExecutesCommandsSequentially()
        {
            var completed = new List<string>();
            _queue.OnCommandCompleted += cmd => completed.Add(cmd.Id);

            _queue.Enqueue(new InstantCommand("a"));
            _queue.Enqueue(new InstantCommand("b"));

            _queue.Tick(_context, 0.016f); // completes "a"
            _queue.Tick(_context, 0.016f); // completes "b"

            Assert.That(completed, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void Tick_WaitsForDurationCommand()
        {
            var completed = new List<string>();
            _queue.OnCommandCompleted += cmd => completed.Add(cmd.Id);

            _queue.Enqueue(new DurationCommand("wait", 1f));
            _queue.Tick(_context, 0.5f);

            Assert.That(completed, Is.Empty);
            Assert.That(_queue.Current, Is.Not.Null);

            _queue.Tick(_context, 0.6f); // total 1.1 >= 1.0
            Assert.That(completed, Is.EqualTo(new[] { "wait" }));
        }

        [Test]
        public void Tick_FailedCommandFiresEvent()
        {
            var failed = new List<string>();
            _queue.OnCommandFailed += cmd => failed.Add(cmd.Id);

            _queue.Enqueue(new FailingCommand("fail"));
            _queue.Tick(_context, 0.016f);

            Assert.That(failed, Is.EqualTo(new[] { "fail" }));
        }

        [Test]
        public void OnQueueEmpty_FiresWhenLastCommandFinishes()
        {
            var emptyFired = false;
            _queue.OnQueueEmpty += () => emptyFired = true;

            _queue.Enqueue(new InstantCommand("a"));
            _queue.Tick(_context, 0.016f);

            Assert.That(emptyFired, Is.True);
        }

        [Test]
        public void Pause_PreventsExecution()
        {
            var completed = new List<string>();
            _queue.OnCommandCompleted += cmd => completed.Add(cmd.Id);

            _queue.Enqueue(new InstantCommand("a"));
            _queue.Pause();
            _queue.Tick(_context, 0.016f);

            Assert.That(completed, Is.Empty);
            Assert.That(_queue.IsPaused, Is.True);
        }

        [Test]
        public void Resume_AllowsExecution()
        {
            var completed = new List<string>();
            _queue.OnCommandCompleted += cmd => completed.Add(cmd.Id);

            _queue.Enqueue(new InstantCommand("a"));
            _queue.Pause();
            _queue.Tick(_context, 0.016f);
            _queue.Resume();
            _queue.Tick(_context, 0.016f);

            Assert.That(completed, Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void Clear_CancelsCurrentAndQueued()
        {
            var cmd1 = new InstantCommand("a");
            var cmd2 = new InstantCommand("b");

            _queue.Enqueue(new DurationCommand("long", 10f));
            _queue.Enqueue(cmd1);
            _queue.Enqueue(cmd2);

            _queue.Tick(_context, 0.016f); // starts "long" as Current
            _queue.Clear();

            Assert.That(_queue.IsEmpty, Is.True);
            Assert.That(cmd1.WasCancelled, Is.True);
            Assert.That(cmd2.WasCancelled, Is.True);
        }

        [Test]
        public void EnqueueRange_AddsMultipleCommands()
        {
            _queue.EnqueueRange(new ICommand[]
            {
                new InstantCommand("a"),
                new InstantCommand("b"),
                new InstantCommand("c")
            });

            Assert.That(_queue.Count, Is.EqualTo(3));
        }

        [Test]
        public void Tick_DoesNothingWhenEmpty()
        {
            // Should not throw
            _queue.Tick(_context, 0.016f);
            Assert.That(_queue.IsEmpty, Is.True);
        }
    }
}
