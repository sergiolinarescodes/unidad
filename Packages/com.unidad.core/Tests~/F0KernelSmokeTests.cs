using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unidad.Core.Abstractions;
using Unidad.Core.Bootstrap;
using Unidad.Core.Content;
using Unidad.Core.DI;
using Unidad.Core.Ticking;

namespace Unidad.Core.Smoke
{
    public sealed class F0KernelSmokeTests
    {
        private interface IFoo { string Name { get; } }

        private sealed class Foo : IFoo
        {
            public Foo(string name) => Name = name;
            public string Name { get; }
        }

        private sealed class StubFoo : IFoo, IStub
        {
            public string Name => "stub";
        }

        private sealed record Row(string Id, int Cost);

        private enum Rarity { Common, Rare }

        private sealed record RarityRow(string Id, Rarity Rarity);

        [Test]
        public void Order_Stub_IsIntMinValue()
        {
            Assert.That(Order.Stub, Is.EqualTo(int.MinValue));
            Assert.That(Order.Default, Is.EqualTo(0));
        }

        [Test]
        public void TickGroup_OrdinalsFrozen()
        {
            Assert.That((int)TickGroup.Input, Is.EqualTo(0));
            Assert.That((int)TickGroup.Simulation, Is.EqualTo(1));
            Assert.That((int)TickGroup.Streaming, Is.EqualTo(2));
            Assert.That((int)TickGroup.Presentation, Is.EqualTo(3));
            Assert.That((int)TickGroup.Rendering, Is.EqualTo(4));
            Assert.That((int)TickGroup.Fixed, Is.EqualTo(5));
        }

        [Test]
        public void Container_HighestOrderWins_TiesLast()
        {
            var container = new ContainerBuilder()
                .AddSingleton<IFoo>(_ => new Foo("high"), 5, typeof(IFoo))
                .AddSingleton<IFoo>(_ => new Foo("low"), 1, typeof(IFoo))
                .Build();
            Assert.That(container.Resolve<IFoo>().Name, Is.EqualTo("high"));

            var tie = new ContainerBuilder()
                .AddSingleton<IFoo>(_ => new Foo("first"))
                .AddSingleton<IFoo>(_ => new Foo("second"))
                .Build();
            Assert.That(tie.Resolve<IFoo>().Name, Is.EqualTo("second"));
        }

        [Test]
        public void Container_StubLosesToReal_AndIsListed()
        {
            var real = new ContainerBuilder()
                .AddSingleton<IFoo>(_ => new Foo("real"))
                .AddStub<IFoo>(_ => new StubFoo())
                .Build();
            Assert.That(real.Resolve<IFoo>().Name, Is.EqualTo("real"));
            Assert.That(real.StubbedContracts(), Is.Empty);
            Assert.That(real.WinningStubs, Is.Empty);

            var stubbed = new ContainerBuilder().AddStub<IFoo>(_ => new StubFoo()).Build();
            Assert.That(stubbed.Resolve<IFoo>(), Is.InstanceOf<IStub>());
            Assert.That(stubbed.StubbedContracts(), Is.EqualTo(new[] { typeof(IFoo) }));
            Assert.That(stubbed.WinningStubs, Is.EqualTo(new[] { typeof(IFoo).FullName }));
        }

        [Test]
        public void ResolveAll_SortedByOrderThenRegistration()
        {
            var container = new ContainerBuilder()
                .AddMany<IFoo>(_ => new Foo("b"), order: 10)
                .AddMany<IFoo>(_ => new Foo("a"), order: -1)
                .AddMany<IFoo>(_ => new Foo("c"), order: 10)
                .Build();
            Assert.That(container.ResolveAll<IFoo>().Select(f => f.Name), Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(container.ResolveAll<IDisposable>(), Is.Empty);
        }

        [Test]
        public void ContentJson_RefusesUnknownMember()
        {
            var unknown = Assert.Throws<ContentException>(() => ContentJson.Parse<Row>("{ \"id\": \"a\", \"cost\": 1, \"colour\": 2 }", "Cards/cards.json"));
            Assert.That(unknown.Message, Does.StartWith("Cards/cards.json: "));
            Assert.That(unknown.Path, Is.EqualTo("Cards/cards.json"));

            Assert.Throws<ContentException>(() => ContentJson.Parse<RarityRow>("{ \"id\": \"a\", \"rarity\": 1 }", "Cards/cards.json"));
            var named = ContentJson.Parse<RarityRow>("{ \"id\": \"a\", \"rarity\": \"Rare\", /* comment */ }", "Cards/cards.json");
            Assert.That(named.Rarity, Is.EqualTo(Rarity.Rare));
        }

        [Test]
        public void Catalogue_DuplicateId_NamesBothFiles()
        {
            var source = new MemoryContentSource(new Dictionary<string, string>
            {
                ["Cards/cards.json"] = "[ { \"id\": \"a\", \"cost\": 1 } ]",
                ["Mods/cards.extra.json"] = "[ { \"id\": \"b\", \"cost\": 2 } ]",
                ["Mods/other.json"] = "[ { \"id\": \"x\", \"cost\": 9 } ]",
            });
            Assert.That(ContentJson.CataloguePaths(source, "cards"), Is.EqualTo(new[] { "Cards/cards.json", "Mods/cards.extra.json" }));
            Assert.That(ContentJson.Catalogue<Row>(source, "cards", r => r.Id).Ids, Is.EqualTo(new[] { "a", "b" }));

            var clash = new MemoryContentSource(new Dictionary<string, string>
            {
                ["Cards/cards.json"] = "[ { \"id\": \"a\", \"cost\": 1 } ]",
                ["Mods/cards.extra.json"] = "[ { \"id\": \"a\", \"cost\": 2 } ]",
            });
            var error = Assert.Throws<ContentException>(() => ContentJson.Catalogue<Row>(clash, "cards", r => r.Id));
            Assert.That(error.Message, Does.Contain("Mods/cards.extra.json").And.Contain("Cards/cards.json"));
        }

        [Test]
        public void EventBus_DeliversInSubscriptionOrder()
        {
            var bus = new Unidad.Core.EventBus.EventBus();
            var seen = new List<string>();
            bus.Subscribe<int>(_ => seen.Add("first"));
            bus.Subscribe<int>(_ => throw new InvalidOperationException("a broken handler"));
            using var third = bus.Subscribe<int>(_ => seen.Add("third"));
            bus.Publish(1);
            Assert.That(seen, Is.EqualTo(new[] { "first", "third" }));
        }

        [Test]
        public void ProcessOverride_AttributeMatchedByName()
        {
            // The analyzers match these markers by simple name in any namespace: the names are the contract.
            Assert.That(typeof(ProcessOverrideAttribute).Name, Is.EqualTo("ProcessOverrideAttribute"));
            Assert.That(typeof(DeterministicSimulationAttribute).Name, Is.EqualTo("DeterministicSimulationAttribute"));
            Assert.That(typeof(BurstSubsetAttribute).Name, Is.EqualTo("BurstSubsetAttribute"));
            Assert.That(typeof(AutoStaticsCleanupAttribute).Name, Is.EqualTo("AutoStaticsCleanupAttribute"));
            Assert.Throws<ArgumentException>(() => new ProcessOverrideAttribute(" "));
        }

        [Test]
        public void ModuleNameConstants_Frozen()
        {
            var core = typeof(Container).Assembly;
            Assert.That(core.GetName().Name, Is.EqualTo("Unidad.Core"));
            Assert.That(core.GetCustomAttribute<UnidadModuleAttribute>().Name, Is.EqualTo("Unidad.Core"));
            Assert.That(InstallerDiscovery.ModuleAssemblies(new[] { typeof(F0KernelSmokeTests).Assembly, core }), Is.EqualTo(new[] { core }));
            Assert.That(InstallerDiscovery.FindInstallerTypes(new[] { core }).Select(t => t.Name), Is.EqualTo(new[] { "GridSystemInstaller" }));
        }
    }
}
