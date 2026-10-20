using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unidad.Core.EventBus;
using Unidad.Samples.Movement;

namespace Unidad.Adapter.Smoke
{
    public sealed class MovementSampleSmokeTests
    {
        private const float Step = 0.02f;

        [Test]
        public void Jump_FromRest_PublishesJumpedAndLeavesGround()
        {
            var (bus, movement) = Spawned();
            movement.Jump(MovementTuning.JumpForce);
            movement.Tick(Step);

            Assert.That(bus.Of<PlayerJumpedEvent>(), Has.Count.EqualTo(1));
            Assert.That(bus.Of<PlayerJumpedEvent>()[0].Force, Is.EqualTo(MovementTuning.JumpForce));
            Assert.That(movement.State.IsGrounded, Is.False);
            Assert.That(movement.State.Position.y, Is.GreaterThan(MovementTuning.RestY));

            movement.Jump(MovementTuning.JumpForce);
            movement.Tick(Step);
            Assert.That(bus.Of<PlayerJumpedEvent>(), Has.Count.EqualTo(1), "no jump while airborne");
        }

        [Test]
        public void WalkOffLeftPlatform_FallsAndReportsFellOffOnce()
        {
            var (bus, movement) = Spawned();
            movement.MoveLeft(MovementTuning.MoveSpeed);
            Run(movement, 1f);
            Assert.That(movement.State.Position.x, Is.LessThan(MovementTuning.LeftPlatformMinX));
            movement.StopHorizontal();
            Run(movement, 3f);

            Assert.That(bus.Of<PlayerFellOffEvent>(), Has.Count.EqualTo(1));
            Assert.That(movement.State.FellOff, Is.True);
            Assert.That(bus.Of<PlayerGroundedChangedEvent>(), Is.EqualTo(new[] { new PlayerGroundedChangedEvent(false) }));
        }

        [Test]
        public void Landing_RequiresDescendingOverPlatform()
        {
            var (bus, movement) = Spawned();
            movement.MoveRight(MovementTuning.MoveSpeed);
            Run(movement, 0.3f);
            Assert.That(movement.State.IsGrounded, Is.True, "held on the left platform");
            Assert.That(movement.State.Position.y, Is.EqualTo(MovementTuning.RestY));

            Run(movement, 0.3f);
            movement.StopHorizontal();
            var x = movement.State.Position.x;
            Assert.That(x, Is.GreaterThan(MovementTuning.LeftPlatformMaxX).And.LessThan(MovementTuning.RightPlatformMinX), "over the hole");
            Run(movement, 3f);

            Assert.That(movement.State.IsGrounded, Is.False);
            Assert.That(bus.Of<PlayerGroundedChangedEvent>(), Is.EqualTo(new[] { new PlayerGroundedChangedEvent(false) }));
        }

        [Test]
        public void Respawn_ResetsToSpawnGrounded()
        {
            var (_, movement) = Spawned();
            movement.MoveLeft(MovementTuning.MoveSpeed);
            Run(movement, 4f);
            Assert.That(movement.State.FellOff, Is.True);

            movement.SpawnLevel();
            movement.Tick(Step);

            Assert.That(movement.State.Position.x, Is.EqualTo(MovementTuning.SpawnX));
            Assert.That(movement.State.Position.y, Is.EqualTo(MovementTuning.RestY));
            Assert.That(movement.State.IsGrounded, Is.True);
            Assert.That(movement.State.FellOff, Is.False);
        }

        [Test]
        public void Walk_DistanceIndependentOfFixedStep()
        {
            var (_, coarse) = Spawned();
            var (_, fine) = Spawned();
            coarse.MoveRight(MovementTuning.MoveSpeed);
            fine.MoveRight(MovementTuning.MoveSpeed);
            for (var i = 0; i < 10; i++) coarse.Tick(Step * 2f);
            for (var i = 0; i < 20; i++) fine.Tick(Step);

            Assert.That(fine.State.Position.x, Is.EqualTo(coarse.State.Position.x).Within(1e-4f));
            Assert.That(fine.State.Position.x, Is.EqualTo(MovementTuning.SpawnX + MovementTuning.MoveSpeed * 0.4f).Within(1e-4f));
        }

        private static (FakeBus, MovementService) Spawned()
        {
            var bus = new FakeBus();
            var movement = new MovementService(bus);
            movement.SpawnLevel();
            return (bus, movement);
        }

        private static void Run(MovementService movement, float seconds)
        {
            for (var t = 0f; t < seconds; t += Step) movement.Tick(Step);
        }

        private sealed class FakeBus : IEventBus
        {
            private readonly List<object> _published = new();

            public List<T> Of<T>() where T : struct => _published.FindAll(e => e is T).ConvertAll(e => (T)e);

            public IDisposable Subscribe<T>(Action<T> handler) where T : struct => throw new NotSupportedException();

            public void Publish<T>(T eventData) where T : struct => _published.Add(eventData);

            public void Unsubscribe<T>(Action<T> handler) where T : struct => throw new NotSupportedException();

            public void ClearAllSubscriptions() { }
        }
    }
}
