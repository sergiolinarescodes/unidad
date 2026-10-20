using System;
using System.Collections.Generic;
using Unidad.Audio;
using Unidad.Core.Abstractions;
using Unidad.Core.DI;
using Unidad.Core.Diagnostics;
using Unidad.Core.EventBus;
using Unidad.Core.Ticking;
using Unidad.Launch;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unidad.Adapter
{
    /// <summary>
    /// The sound's Unity half and the only owner of AudioSources. It mirrors the sound events and decides nothing: one-shots come
    /// out of a pool, music has two sources that crossfade, speech has one source fed by blips synthesized in memory (each cuts
    /// the last). Ticked while paused, so the pause menu's clicks play. Volumes are linear; <c>mute</c> sets every one to 0.
    /// </summary>
    internal sealed class SoundHost : MonoBehaviour, ITickable
    {
        private readonly Dictionary<string, AudioClip> _clips = new();
        private readonly Dictionary<VoiceBlip, AudioClip> _blips = new();
        private readonly ActiveVoices<AudioSource> _active = new();
        private readonly float[] _levels = { 1f, 1f, 1f, 1f, 1f };
        private AssetLibrary _assets;
        private ComponentPool<AudioSource> _voices;
        private AudioSource _musicA, _musicB, _playing, _rising, _falling, _speech;
        private float _fadeSeconds, _fadeElapsed, _risingTarget, _fallingFrom, _playingLevel;
        private bool _fading, _muted;
        private IDisposable[] _subscriptions = Array.Empty<IDisposable>();

        public static SoundHost Bind(Container container, IGameObjectFactory factory, ITickRegistry ticks, ScenePreparation preparation,
            AssetLibrary assets, LaunchArgs launch)
        {
            var host = factory.Create<SoundHost>("[Sound]");
            host._assets = assets;
            host._muted = launch.IsOn(Unidad.Audio.AudioSettings.Mute);
            host.Build(factory, container.Resolve<IEventBus>());
            var catalogue = container.Resolve<SoundCatalog>();
            var queued = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in catalogue.Ids)
                if (catalogue.TryGet(id, out var cue) && !cue.IsSilent && queued.Add(cue.Stream))
                    preparation.Enqueue("audio:" + cue.Stream, () => host.Clip(cue.Stream));
            ticks.Register(host, TickGroup.Presentation, runWhilePaused: true);
            return host;
        }

        public void SetBusLevel(AudioBus bus, float linear)
        {
            _levels[(int)bus] = Math.Clamp(linear, 0f, 1f);
            if (_playing != null && !_fading) _playing.volume = Mix(_playingLevel, AudioBus.Music);
            _speech.volume = Mix(VoiceTuning.VoiceVolumeLinear, AudioBus.Voice);
        }

        public void Tick(float deltaTime)
        {
            if (_voices == null) return;
            _active.Sweep(source => source != null && source.isPlaying, _voices.Return);
            if (!_fading) return;
            _fadeElapsed += deltaTime;
            Settle(MusicFade.Progress(_fadeElapsed, _fadeSeconds));
        }

        private void Build(IGameObjectFactory factory, IEventBus bus)
        {
            var voices = factory.Create("Voices", transform).transform;
            _voices = new ComponentPool<AudioSource>(() => Source(factory, "Voice", voices), voices, reset: source =>
            {
                source.Stop();
                source.clip = null;
            });
            _voices.Prewarm(AudioTuning.OneShotVoices);
            _musicA = Source(factory, "MusicA", transform);
            _musicB = Source(factory, "MusicB", transform);
            _musicA.loop = _musicB.loop = true;
            _speech = Source(factory, "Speech", transform);
            _speech.volume = Mix(VoiceTuning.VoiceVolumeLinear, AudioBus.Voice);
            _subscriptions = new[]
            {
                bus.Subscribe<SoundPlayedEvent>(played => PlayOneShot(played.Cue)),
                bus.Subscribe<MusicStartedEvent>(started => StartMusic(started.Cue, started.CrossfadeSeconds)),
                bus.Subscribe<MusicStoppedEvent>(stopped => FadeOut(stopped.FadeSeconds)),
                bus.Subscribe<VoiceBlipEvent>(blip => Speak(blip.Blip)),
                bus.Subscribe<VoiceStoppedEvent>(_ => _speech.Stop()),
            };
        }

        private static AudioSource Source(IGameObjectFactory factory, string name, Transform parent)
        {
            var source = factory.Create<AudioSource>(name, parent);
            source.playOnAwake = false;
            source.volume = 0f;
            return source;
        }

        private float Mix(float level, AudioBus bus) => BusMix.Volume(level, bus, _levels, _muted);

        private void PlayOneShot(SoundCue cue)
        {
            var clip = Clip(cue.Stream);
            if (clip == null || !_voices.TryAcquire(out var source)) return;
            source.clip = clip;
            source.volume = Mix(cue.BaseVolumeLinear, cue.Bus);
            source.Play();
            _active.Add(source);
        }

        private void Speak(VoiceBlip blip)
        {
            if (!_blips.TryGetValue(blip, out var clip))
            {
                // Synthesized once per distinct syllable and kept: a conversation reuses a few dozen.
                var samples = AnimaleseSynth.Render(blip, VoiceTuning.SampleRate);
                clip = AudioClip.Create("blip", samples.Length, 1, VoiceTuning.SampleRate, false);
                clip.SetData(samples, 0);
                _blips[blip] = clip;
            }
            _speech.clip = clip;
            _speech.Play();
        }

        private void StartMusic(SoundCue cue, float crossfadeSeconds)
        {
            var clip = Clip(cue.Stream);
            if (clip == null) return;
            var next = _playing == _musicA ? _musicB : _musicA;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            _falling = _playing;
            _fallingFrom = _falling == null ? 0f : _playingLevel;
            _rising = next;
            _risingTarget = Math.Clamp(cue.BaseVolumeLinear, 0f, 1f);
            _playing = next;
            _playingLevel = _risingTarget;
            StartFade(crossfadeSeconds);
        }

        private void FadeOut(float fadeSeconds)
        {
            _falling = _playing;
            _fallingFrom = _falling == null ? 0f : _playingLevel;
            _rising = null;
            _playing = null;
            StartFade(fadeSeconds);
        }

        private void StartFade(float seconds)
        {
            _fadeSeconds = seconds;
            _fadeElapsed = 0f;
            _fading = true;
            // A zero-length hand-over lands in this call rather than on the next tick.
            if (seconds <= 0f) Settle(1f);
        }

        private void Settle(float t)
        {
            var levels = MusicFade.Settle(t, _risingTarget, _fallingFrom);
            if (_rising != null) _rising.volume = Mix(levels.Rising, AudioBus.Music);
            if (_falling != null && _falling != _rising) _falling.volume = Mix(levels.Falling, AudioBus.Music);
            if (!levels.Done) return;
            if (_falling != null && _falling != _rising) _falling.Stop();
            _falling = _rising = null;
            _fading = false;
        }

        /// <summary>A clip loaded once and remembered, a miss included, so a missing key is reported one time.</summary>
        private AudioClip Clip(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_clips.TryGetValue(key, out var cached)) return cached;
            if (!_assets.TryLoad<AudioClip>(key, out var clip)) Log.Warning($"[Sound] No audio clip at {key}.");
            _clips[key] = clip;
            return clip;
        }

        private void OnDestroy()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions = Array.Empty<IDisposable>();
            _voices?.Dispose();
            _voices = null;
            foreach (var clip in _blips.Values) UnityObject.Destroy(clip);
            _blips.Clear();
        }
    }
}
