using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unidad.Core.Abstractions;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;
using Unidad.Core.Grid;
using Unidad.Core.Patterns.Modifier;
using Unidad.Core.Primitives;
using Unidad.Core.Random;
using Unidad.Core.Ticking;

namespace Unidad.Core.Smoke
{
    // Declared Z before A so the discovery sort, not declaration order, decides.
    public sealed class ZRuntimeSmokeInstaller : ISystemInstaller { public void Install(ContainerBuilder builder) { } }
    public sealed class ARuntimeSmokeInstaller : ISystemInstaller { public void Install(ContainerBuilder builder) { } }

    public sealed class CoreRuntimeSmokeTests
    {
        public interface IFoo { string Name { get; } }

        private sealed class Foo : IFoo
        {
            public Foo(string name) => Name = name;
            public string Name { get; }
        }

        private sealed class StubFoo : IFoo, IStub { public string Name => "stub"; }

        public interface IAction { string Id { get; } }
        private sealed class RealAction : IAction { public RealAction(string id) => Id = id; public string Id { get; } }
        private sealed class OtherRealAction : IAction { public OtherRealAction(string id) => Id = id; public string Id { get; } }
        private sealed class StubAction : IAction, IStub { public StubAction(string id) => Id = id; public string Id { get; } }

        public sealed class A { public A(B b) { } }
        public sealed class B { public B(A a) { } }

        private sealed class Tracked : IDisposable
        {
            private readonly List<string> _log;
            public Tracked(string name, List<string> log) { Name = name; _log = log; }
            public string Name { get; }
            public void Dispose() => _log.Add(Name);
        }

        private sealed class Ticker : ITickable
        {
            public readonly List<float> Deltas = new();
            public void Tick(float deltaTime) => Deltas.Add(deltaTime);
        }

        private sealed class Thrower : ITickable
        {
            public int Calls;
            public void Tick(float deltaTime) { Calls++; throw new InvalidOperationException("boom"); }
        }

        private sealed class Add : IModifier<int>
        {
            public Add(string id, int priority, int amount) { Id = id; Priority = priority; _amount = amount; }
            private readonly int _amount;
            public string Id { get; }
            public int Priority { get; }
            public bool IsActive => true;
            public int Apply(int value) => value * 10 + _amount;
        }

        [Test]
        public void SingleBinding_HighestOrderWins_EitherRegistrationOrder()
        {
            var stubFirst = new ContainerBuilder()
                .AddStub<IFoo>(_ => new StubFoo())
                .AddSingleton<IFoo>(_ => new Foo("real"))
                .Build();
            var realFirst = new ContainerBuilder()
                .AddSingleton<IFoo>(_ => new Foo("real"))
                .AddStub<IFoo>(_ => new StubFoo())
                .Build();

            Assert.That(stubFirst.Resolve<IFoo>().Name, Is.EqualTo("real"));
            Assert.That(realFirst.Resolve<IFoo>().Name, Is.EqualTo("real"));
            Assert.That(stubFirst.WinningStubs, Is.Empty);
            Assert.That(realFirst.WinningStubs, Is.Empty);
        }

        [Test]
        public void SingleBinding_OnlyStub_ResolvesAndIsReported()
        {
            var container = new ContainerBuilder().AddStub<IFoo>(_ => new StubFoo()).Build();

            Assert.That(container.Resolve<IFoo>().Name, Is.EqualTo("stub"));
            Assert.That(container.WinningStubs, Is.EqualTo(new[] { typeof(IFoo).FullName }));
        }

        [Test]
        public void SingleBinding_SameOrder_LastRegistrationWins()
        {
            var container = new ContainerBuilder()
                .AddSingleton<IFoo>(_ => new Foo("first"), 3, typeof(IFoo))
                .AddSingleton<IFoo>(_ => new Foo("second"), 3, typeof(IFoo))
                .Build();

            Assert.That(container.Resolve<IFoo>().Name, Is.EqualTo("second"));
        }

        [Test]
        public void ResolveAll_SortedByOrderThenRegistration()
        {
            var container = new ContainerBuilder()
                .AddMany<IFoo>(_ => new Foo("late"), 10)
                .AddMany<IFoo>(_ => new Foo("tie-1"))
                .AddMany<IFoo>(_ => new Foo("early"), -5)
                .AddMany<IFoo>(_ => new Foo("tie-2"))
                .Build();

            Assert.That(container.ResolveAll<IFoo>().Select(f => f.Name),
                        Is.EqualTo(new[] { "early", "tie-1", "tie-2", "late" }));
        }

        [Test]
        public void ResolveKeyed_RealReplacesStub_TwoRealsThrow_StubIdsReported()
        {
            var container = new ContainerBuilder()
                .AddMany<IAction>(_ => new StubAction("spawn"))
                .AddMany<IAction>(_ => new StubAction("give"))
                .AddMany<IAction>(_ => new RealAction("spawn"))
                .Build();

            var keyed = container.ResolveKeyed<IAction>(a => a.Id);

            Assert.That(keyed.Get("spawn"), Is.InstanceOf<RealAction>());
            Assert.That(keyed.Get("give"), Is.InstanceOf<StubAction>());
            Assert.That(container.ResolveKeyed<IAction>(a => a.Id), Is.SameAs(keyed));
            Assert.That(container.WinningStubs, Is.EqualTo(new[] { typeof(IAction).FullName + ":give" }));

            var twice = new ContainerBuilder()
                .AddMany<IAction>(_ => new RealAction("spawn"))
                .AddMany<IAction>(_ => new OtherRealAction("spawn"))
                .Build();
            var ex = Assert.Throws<InvalidOperationException>(() => twice.ResolveKeyed<IAction>(a => a.Id));
            Assert.That(ex.Message, Is.EqualTo(
                $"IAction id 'spawn' is bound twice: {typeof(RealAction).FullName} and {typeof(OtherRealAction).FullName}."));
        }

        [Test]
        public void Cycle_ThrowsWithChain()
        {
            var container = new ContainerBuilder().AddSingletonType(typeof(A)).AddSingletonType(typeof(B)).Build();

            var ex = Assert.Throws<InvalidOperationException>(() => container.Resolve<A>());

            Assert.That(ex.Message, Is.EqualTo("Circular dependency detected: A -> B -> A"));
        }

        [Test]
        public void Dispose_ReverseCreationOrder_CallerOwnedUntouched()
        {
            var log = new List<string>();
            var callerOwned = new Tracked("caller", log);
            var container = new ContainerBuilder()
                .AddSingleton(callerOwned)
                .AddSingleton(_ => new Tracked("first", log), typeof(IDisposable))
                .AddSingleton<Tracked>(c => { c.Resolve<IDisposable>(); return new Tracked("second", log); }, typeof(object))
                .Build();
            container.Resolve<object>();

            container.Dispose();

            Assert.That(log, Is.EqualTo(new[] { "second", "first" }));
            Assert.Throws<ObjectDisposedException>(() => container.Resolve<object>());
        }

        [Test]
        public void Discovery_FiltersByUnidadModule_SortsByModuleThenTypeName()
        {
            var smoke = typeof(CoreRuntimeSmokeTests).Assembly;
            var core = typeof(Container).Assembly;
            var nunit = typeof(Assert).Assembly;

            // The smoke assembly carries no [UnidadModule] (F0's ModuleNameConstants_Frozen pins that), so it sorts by its simple name.
            Assert.That(InstallerDiscovery.ModuleAssemblies(new[] { smoke, nunit, core }), Is.EqualTo(new[] { core }));

            var found = InstallerDiscovery.FindInstallerTypes(new[] { smoke, core })
                .Where(t => t == typeof(GridSystemInstaller) || t.Name.EndsWith("RuntimeSmokeInstaller", StringComparison.Ordinal))
                .ToList();
            Assert.That(found, Is.EqualTo(new[] { typeof(GridSystemInstaller), typeof(ARuntimeSmokeInstaller), typeof(ZRuntimeSmokeInstaller) }));
        }

        [Test]
        public void TickSchedule_PausedRunsOnlyRunWhilePaused_WithUnscaledDelta_FaultDisablesOnce()
        {
            var schedule = new TickSchedule("smoke");
            var game = new Ticker();
            var ui = new Ticker();
            var fixedRate = new Ticker();
            var thrower = new Thrower();
            schedule.Add(new TickBinding(game, TickGroup.Simulation));
            schedule.Add(new TickBinding(ui, TickGroup.Presentation, RunWhilePaused: true));
            schedule.Add(fixedRate, TickGroup.Simulation, 10f);
            schedule.Add(thrower, TickGroup.Input);

            schedule.Step(0.05f, 0.5f, paused: true);
            schedule.Step(0.06f, 0.1f, paused: false);
            schedule.Step(0.05f, 0.5f, paused: false);

            Assert.That(game.Deltas, Is.EqualTo(new[] { 0.06f, 0.05f }));
            Assert.That(ui.Deltas, Is.EqualTo(new[] { 0.5f, 0.1f, 0.5f }));
            // The paused step did not feed the accumulator: 0.06 + 0.05 pays for exactly one 0.1 s step.
            Assert.That(fixedRate.Deltas, Is.EqualTo(new[] { 0.1f }));
            Assert.That(thrower.Calls, Is.EqualTo(1));
            Assert.That(schedule.Faults.Select(f => f.Name), Is.EqualTo(new[] { nameof(Thrower) }));
            Assert.That(schedule.Contains(thrower), Is.False);

            schedule.StepGroup(TickGroup.Presentation, 0.2f, 0.3f, paused: false);
            Assert.That(ui.Deltas.Last(), Is.EqualTo(0.3f));
            Assert.That(game.Deltas.Count, Is.EqualTo(2));
        }

        [Test]
        public void SeedSource_DeriveIsSaltThenMix_ModifierStackEqualPriorityKeepsInsertionOrder()
        {
            // FNV-1a 32 of "a": (2166136261 ^ 0x61) * 16777619 mod 2^32.
            Assert.That(SeedMix.Salt("a"), Is.EqualTo(unchecked((int)0xE40C292Cu)));
            var source = new SeedSource(42);
            Assert.That(source.Derive("battle"), Is.EqualTo(SeedMix.Derive(42, SeedMix.Salt("battle"))));
            Assert.That(source.Derive("battle"), Is.Not.EqualTo(source.Derive("shop")));

            var stack = new ModifierStack<int>();
            stack.Add(new Add("x", 0, 1));
            stack.Add(new Add("y", 5, 2));
            stack.Add(new Add("z", 0, 3));
            stack.Add(new Add("w", 0, 4));
            // Priority 5 first, then the three zeros in insertion order: ((((0*10+2)*10+1)*10+3)*10+4).
            Assert.That(stack.Evaluate(0), Is.EqualTo(2134));
            stack.Add(new Add("v", 0, 5));
            Assert.That(stack.Evaluate(0), Is.EqualTo(21345));

            Assert.That(DetMath.Exp(1d), Is.EqualTo(Math.E).Within(1e-15));
            Assert.That(DetMath.Exp(-745.3), Is.EqualTo(0d));
            // Subnormal result: the scale goes through the split power-of-two path.
            Assert.That(DetMath.Exp(-740d), Is.EqualTo(Math.Exp(-740d)).Within(1).Percent);
        }
    }
}
