using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unidad.Audio;
using Unidad.Core.Content;
using Unidad.Core.EventBus;

namespace Unidad.Audio.Smoke
{
    public sealed class AudioSmokeTests
    {
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

        [Test]
        public void SilentRowsAndUnknownIdsPublishNothing()
        {
            var source = new MemoryContentSource(new Dictionary<string, string>
            {
                ["Ui/sounds.ui.json"] = "[ { \"id\": \"ui.click\", \"stream\": \"Audio/ui_click\", \"bus\": \"Ui\", \"volume\": 0.8 }, { \"id\": \"card.throw\" } ]",
                ["Room/sounds.json"] = "[ { \"id\": \"room.knock\", \"stream\": \"Audio/knock\" } ]",
            });
            var catalog = SoundCatalog.Load(source);
            Assert.That(catalog.Count, Is.EqualTo(3));

            var bus = new EventBus();
            var played = new List<SoundCue>();
            var unknown = new List<string>();
            bus.Subscribe<SoundPlayedEvent>(e => played.Add(e.Cue));
            bus.Subscribe<UnknownSoundEvent>(e => unknown.Add(e.Id));
            var sound = new SoundService(bus, catalog);

            sound.Play(UiCues.Click);
            sound.PlaySfx(new SoundId("card.throw"));
            sound.Play(new SoundId("nope"));
            sound.Play(new SoundId("nope"));
            sound.PlayMusic(new MusicId("theme"));

            Assert.That(played.Count, Is.EqualTo(1));
            Assert.That(played[0].Bus, Is.EqualTo(AudioBus.Ui));
            Assert.That(played[0].BaseVolumeLinear, Is.EqualTo(0.8f));
            Assert.That(unknown, Is.EqualTo(new[] { "nope", "theme" }));
            Assert.That(sound.CurrentMusic, Is.Null);

            var loud = new MemoryContentSource(new Dictionary<string, string> { ["Ui/sounds.ui.json"] = "[ { \"id\": \"x\", \"volume\": 2 } ]" });
            Assert.That(Assert.Throws<ContentException>(() => SoundCatalog.Load(loud)).Message, Does.StartWith("Ui/sounds.ui.json"));
            var stray = new MemoryContentSource(new Dictionary<string, string> { ["Ui/sounds.ui.json"] = "[ { \"id\": \"x\", \"loud\": true } ]" });
            Assert.Throws<ContentException>(() => SoundCatalog.Load(stray));
            Assert.That(VolumeScale.ToDecibels(1f), Is.EqualTo(0f));
            Assert.That(VolumeScale.ToDecibels(1f, muted: true), Is.EqualTo(AudioTuning.SilenceDecibels));
            Assert.That(AnimaleseSynth.Render(Animalese.Blip(Animalese.For("someone"), 'k', 'a')),
                Is.EqualTo(AnimaleseSynth.Render(Animalese.Blip(Animalese.For("someone"), 'k', 'a'))));
        }
    }
}
