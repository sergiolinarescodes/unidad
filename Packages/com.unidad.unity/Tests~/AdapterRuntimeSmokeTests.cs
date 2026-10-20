using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;

namespace Unidad.Adapter.Smoke
{
    public sealed class AdapterRuntimeSmokeTests
    {
        [Test]
        public void PlayerLoopPlan_InsertsAfterInputUpdateAndBeforeScriptRuns()
        {
            Assert.That(PlayerLoopPlan.InsertIndex(new[] { "EarlyX", "NewInputUpdate", "Y" }, PlayerLoopPlan.Input, null), Is.EqualTo(2));
            Assert.That(PlayerLoopPlan.InsertIndex(new[] { "A", "ScriptRunBehaviourUpdate" }, PlayerLoopPlan.Update, null), Is.EqualTo(1));
            Assert.That(PlayerLoopPlan.InsertIndex(new[] { "ScriptRunBehaviourFixedUpdate" }, PlayerLoopPlan.Fixed, null), Is.EqualTo(0));
        }

        [Test]
        public void PlayerLoopPlan_MissingAnchorAppendsMissingPhaseThrows()
        {
            var warnings = new List<string>();
            Assert.That(PlayerLoopPlan.InsertIndex(new[] { "A", "B" }, PlayerLoopPlan.LateUpdate, warnings.Add), Is.EqualTo(2));
            Assert.That(warnings, Is.EqualTo(new[] { "[TickRunner] ScriptRunBehaviourLateUpdate not found in PreLateUpdate; appended." }));

            Assert.That(PlayerLoopPlan.PhaseIndex(new[] { "Initialization", "PreUpdate" }, "PreUpdate"), Is.EqualTo(1));
            var missing = Assert.Throws<InvalidOperationException>(() => PlayerLoopPlan.PhaseIndex(new[] { "Initialization" }, "FixedUpdate"));
            Assert.That(missing.Message, Is.EqualTo("PlayerLoop has no FixedUpdate phase."));
        }

        [Test]
        public void PlayerLoopPlan_StripRemovesOnlyUnidadTickByFullName()
        {
            var systems = new[] { "Foo.UnidadTickX", "Unidad.Adapter.UnidadTick+Input", "Bar", "Unidad.Adapter.UnidadTick+Fixed", null };
            Assert.That(PlayerLoopPlan.Strip(systems, name => name), Is.EqualTo(new[] { "Foo.UnidadTickX", "Bar", null }));
            Assert.That(PlayerLoopPlan.Strip(new[] { "Foo", "Unidad.Adapter.UnidadTicker" }, name => name), Is.Null, "nothing ours: no rewrite");
            Assert.That(PlayerLoopPlan.IsMarker(typeof(UnidadTickName.Nested).FullName), Is.False);
        }

        [Test]
        public void AssetShelf_SealedColdLoadThrowsNamingKey_ScopeAllows()
        {
            var shelf = new AssetShelf();
            var loads = 0;
            shelf.Seal();
            var cold = Assert.Throws<InvalidOperationException>(() => shelf.Get("Props/Lamp", typeof(string), () => { loads++; return "lamp"; }));
            Assert.That(cold.Message, Does.Contain("'Props/Lamp'"));
            Assert.That(loads, Is.Zero);

            using (shelf.AllowColdLoads())
            {
                using (shelf.AllowColdLoads()) { }
                Assert.That(shelf.Get("Props/Lamp", typeof(string), () => { loads++; return "lamp"; }), Is.EqualTo("lamp"), "outer scope still open");
            }
            Assert.That(shelf.Get("Props/Lamp", typeof(string), () => { loads++; return "other"; }), Is.EqualTo("lamp"), "cached after the scope");
            Assert.That(loads, Is.EqualTo(1));
            shelf.BeginScene();
            Assert.That(shelf.IsSealed, Is.False);
        }

        [Test]
        public void AssetShelf_MissIsCachedAndTypeKeysAreSeparate()
        {
            var shelf = new AssetShelf();
            var loads = 0;
            shelf.Get("Ui/Panel", typeof(string), () => { loads++; return "a"; });
            shelf.Get("Ui/Panel", typeof(int[]), () => { loads++; return new int[0]; });
            Assert.That(loads, Is.EqualTo(2));
            Assert.That(shelf.Count, Is.EqualTo(2));

            var misses = 0;
            Assert.That(shelf.TryGet("Missing", typeof(string), () => { misses++; return null; }, out _), Is.False);
            Assert.That(shelf.TryGet("Missing", typeof(string), () => { misses++; return null; }, out _), Is.False);
            Assert.That(misses, Is.EqualTo(1));
            var thrown = Assert.Throws<InvalidOperationException>(() => shelf.Get("Missing", typeof(string), () => null));
            Assert.That(thrown.Message, Is.EqualTo("Asset did not load: Missing (String)."));
        }

        [Test]
        public void PoolCore_TryAcquireNeverAllocates_ForeignReturnThrows()
        {
            var created = 0;
            var pool = NewPool(() => { created++; return new Item(); }, new List<(Item, bool)>());
            Assert.That(pool.TryAcquire(out var none), Is.False);
            Assert.That(none, Is.Null);
            Assert.That(created, Is.Zero);

            Assert.Throws<ArgumentException>(() => pool.Return(new Item()));
            var item = pool.Acquire();
            Assert.That(created, Is.EqualTo(1));
            pool.Return(item);
            pool.Return(item);
            Assert.That(pool.AvailableCount, Is.EqualTo(1), "double return ignored");
            Assert.That(item.Returns, Is.EqualTo(1));
            Assert.That(pool.TryAcquire(out var again), Is.True);
            Assert.That(again, Is.SameAs(item));
            Assert.That(item.Acquires, Is.EqualTo(2));
        }

        [Test]
        public void PoolCore_PrewarmParksEntriesInactive()
        {
            var activations = new List<(Item, bool)>();
            var pool = NewPool(() => new Item(), activations);
            pool.Prewarm(3);
            Assert.That(activations.Count, Is.EqualTo(3));
            Assert.That(activations.TrueForAll(change => !change.Item2), Is.True);
            Assert.That(pool.AvailableCount, Is.EqualTo(3));
            Assert.That(pool.TotalCount, Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Prewarm(-1));
            pool.Dispose();
            Assert.Throws<ObjectDisposedException>(() => pool.TryAcquire(out _));
        }

        [Test]
        public void InstallerOrderLog_ListsFullNamesInGivenOrder()
        {
            var line = InstallerOrderLog.Line(new ISystemInstaller[] { new SecondInstaller(), new FirstInstaller() });
            Assert.That(line, Is.EqualTo(
                "[Boot] 2 installers: Unidad.Adapter.Smoke.AdapterRuntimeSmokeTests+SecondInstaller, Unidad.Adapter.Smoke.AdapterRuntimeSmokeTests+FirstInstaller"));
            Assert.That(InstallerOrderLog.StubWarning(Array.Empty<string>()), Is.Null);
            Assert.That(InstallerOrderLog.StubWarning(new[] { "A.IShop", "B.IDeck" }), Is.EqualTo("[Boot] 2 stubs still win: A.IShop, B.IDeck"));
            Assert.That(InstallerOrderLog.StrictFailure(new[] { "A.IShop" }), Is.EqualTo("strict: 1 stub(s) still win: A.IShop."));
        }

        private static PoolCore<Item> NewPool(Func<Item> create, List<(Item, bool)> activations) =>
            new(create, item => !item.Destroyed, (item, active) => activations.Add((item, active)), null, null,
                item => item.Destroyed = true, "PoolCore<Item>");

        private sealed class Item : Unidad.Core.Abstractions.IPoolable
        {
            public bool Destroyed;
            public int Acquires, Returns;
            public void OnAcquireFromPool() => Acquires++;
            public void OnReturnToPool() => Returns++;
        }

        private sealed class FirstInstaller : ISystemInstaller
        {
            public void Install(ContainerBuilder builder) { }
        }

        private sealed class SecondInstaller : ISystemInstaller
        {
            public void Install(ContainerBuilder builder) { }
        }
    }

    internal static class UnidadTickName
    {
        public struct Nested { }
    }
}
