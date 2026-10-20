using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Unidad.Core.EventBus;
using Unidad.Save;

namespace Unidad.Save.Smoke
{
    public sealed class SaveSmokeTests
    {
        private sealed class MemoryStore : ISaveStore
        {
            public string Text;
            public int Writes;
            public string Location => "memory";
            public string Read() => Text;

            public void Write(string text)
            {
                Text = text;
                Writes++;
            }
        }

        private sealed class Counter : ISaveParticipant
        {
            public int Value, Applied;
            public bool Refuse;

            public Counter(string key) => Key = key;

            public string Key { get; }

            public JsonNode Capture() => new JsonObject { ["value"] = Value };

            public Action Read(JsonNode section)
            {
                if (Refuse) throw new SaveFormatException("refused.");
                int value = SaveRead.Int(SaveRead.Object(section, Key), "value");
                return () =>
                {
                    Value = value;
                    Applied++;
                };
            }
        }

        private readonly record struct Advanced;

        private sealed class EventBus : IEventBus
        {
            private readonly List<(Type Type, Delegate Handler)> _handlers = new();

            public IDisposable Subscribe<T>(Action<T> handler) where T : struct
            {
                _handlers.Add((typeof(T), handler));
                return new Gone(() => Unsubscribe(handler));
            }

            public void Publish<T>(T eventData) where T : struct
            {
                foreach (var (type, handler) in _handlers.ToArray())
                    if (type == typeof(T)) ((Action<T>)handler)(eventData);
            }

            public void Unsubscribe<T>(Action<T> handler) where T : struct => _handlers.RemoveAll(h => h.Handler.Equals(handler));

            public void ClearAllSubscriptions() => _handlers.Clear();

            private sealed class Gone(Action action) : IDisposable
            {
                public void Dispose() => action();
            }
        }

        private const string Slot = "{ \"version\": 1, \"sections\": { \"a\": { \"value\": 4 }, \"b\": { \"value\": 5 }, \"zz\": { \"kept\": true } } }";

        [Test]
        public void LoadIsWholeOrNothing()
        {
            var store = new MemoryStore { Text = Slot };
            var save = new SaveService(new EventBus(), store);
            var a = new Counter("a");
            save.Register(a);
            save.Register(new Counter("b") { Refuse = true });
            Assert.Throws<ArgumentException>(() => save.Register(new Counter("a")));

            var result = save.Load();
            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.Unreadable));
            Assert.That(result.Message, Does.Contain("section 'b'"));
            Assert.That(a.Applied, Is.EqualTo(0));
            Assert.That(save.CanWrite, Is.False);
            Assert.That(save.Save(), Is.False);
            Assert.That(store.Writes, Is.EqualTo(0));
            Assert.That(save.HasSave, Is.True);
        }

        [Test]
        public void UnknownSectionsSurvive_FutureVersionRefused()
        {
            var store = new MemoryStore { Text = Slot };
            var save = new SaveService(new EventBus(), store);
            var a = new Counter("a");
            save.Register(a);
            Assert.That(save.Load().Outcome, Is.EqualTo(SaveLoadOutcome.Loaded));
            Assert.That(a.Value, Is.EqualTo(4));
            a.Value = 9;
            Assert.That(save.Save(), Is.True);
            var written = JsonNode.Parse(store.Text).AsObject()["sections"].AsObject();
            Assert.That((bool)written["zz"]["kept"], Is.True);
            Assert.That((int)written["b"]["value"], Is.EqualTo(5));
            Assert.That((int)written["a"]["value"], Is.EqualTo(9));

            var future = new MemoryStore { Text = "{ \"version\": 2, \"sections\": {} }" };
            var refused = new SaveService(new EventBus(), future);
            Assert.That(refused.Load().Outcome, Is.EqualTo(SaveLoadOutcome.RefusedFutureVersion));
            Assert.That(refused.Save(), Is.False);
            Assert.That(future.Writes, Is.EqualTo(0));
            Assert.That(refused.StartFresh().Message, Is.EqualTo("fresh start"));
            Assert.That(refused.CanWrite, Is.True);
        }

        [Test]
        public void AutosaveAfterCoalescesPerTick()
        {
            var bus = new EventBus();
            var store = new MemoryStore();
            var save = new SaveService(bus, store);
            save.StartFresh();
            save.AutosaveAfter<Advanced>();
            bus.Publish(new Advanced());
            bus.Publish(new Advanced());
            bus.Publish(new Advanced());
            Assert.That(store.Writes, Is.EqualTo(0));
            save.Tick(0.016f);
            save.Tick(0.016f);
            Assert.That(store.Writes, Is.EqualTo(1));

            var periodic = new AutosaveService(bus, save, new AutosaveTuning { PeriodSeconds = 10 });
            periodic.PeriodicWhile(() => true);
            for (int i = 0; i < 9; i++) periodic.Tick(1f);
            Assert.That(store.Writes, Is.EqualTo(1));
            periodic.Tick(1f);
            Assert.That(store.Writes, Is.EqualTo(2));
            Assert.That(periodic.SettledSeconds, Is.EqualTo(0));
        }

        [Test]
        public void PeriodZeroDisablesAutosave()
        {
            var bus = new EventBus();
            var store = new MemoryStore();
            var save = new SaveService(bus, store);
            save.StartFresh();
            var autosave = new AutosaveService(bus, save, new AutosaveTuning { PeriodSeconds = 0 });
            autosave.PeriodicWhile(() => true);
            for (int i = 0; i < 1000; i++) autosave.Tick(1f);
            Assert.That(store.Writes, Is.EqualTo(0));
            autosave.On<Advanced>();
            bus.Publish(new Advanced());
            autosave.Tick(0f);
            Assert.That(store.Writes, Is.EqualTo(1));
        }

        [Test]
        public void FileStoreRoundTripKeepsBackup()
        {
            var folder = Path.Combine(Path.GetTempPath(), "unidad-save-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileSaveStore(Path.Combine(folder, FileSaveStore.SlotFile));
                Assert.That(store.Read(), Is.Null);
                store.Write("{\"n\":1}");
                store.Write("{\"n\":2}");
                Assert.That(store.Read(), Is.EqualTo("{\"n\":2}"));
                Assert.That(File.ReadAllText(store.Location + FileSaveStore.BackupSuffix), Is.EqualTo("{\"n\":1}"));
                Assert.That(File.Exists(store.Location + FileSaveStore.TempSuffix), Is.False);
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
