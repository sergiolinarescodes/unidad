using System;
using System.Collections.Generic;
using Unidad.Core.Diagnostics;
using Unidad.Core.EventBus;

namespace Unidad.Audio
{
    /// <summary>
    /// Turns a call into an event. It owns no player and loads no file: the adapter's SoundHost plays the stream, so every test
    /// can call these verbs with no audio device attached.
    /// </summary>
    internal sealed class SoundService : ISoundService
    {
        private readonly IEventBus _bus;
        private readonly SoundCatalog _catalog;
        private readonly HashSet<string> _alreadyNoted = new(StringComparer.Ordinal);
        private MusicId? _current;
        private bool _speaking;

        public SoundService(IEventBus bus, SoundCatalog catalog)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _catalog = catalog ?? SoundCatalog.Empty();
        }

        public MusicId? CurrentMusic => _current;

        public void Play(SoundId id) => PlayOneShot(id.Value, null);

        public void PlaySfx(SoundId id) => PlayOneShot(id.Value, AudioBus.Sfx);

        public void PlayUi(SoundId id) => PlayOneShot(id.Value, AudioBus.Ui);

        public void PlayMusic(MusicId id)
        {
            if (_current.HasValue && _current.Value.Value == id.Value) return;
            if (!TryResolve(id.Value, AudioBus.Music, out var cue)) return;
            var crossfade = _current.HasValue ? AudioTuning.MusicCrossfadeSeconds : 0f;
            _current = id;
            _bus.Publish(new MusicStartedEvent(cue, crossfade));
        }

        public void StopMusic()
        {
            if (!_current.HasValue) return;
            _current = null;
            _bus.Publish(new MusicStoppedEvent(AudioTuning.MusicFadeOutSeconds));
        }

        public void PlayVoice(VoiceBlip blip)
        {
            _speaking = true;
            _bus.Publish(new VoiceBlipEvent(blip));
        }

        public void StopVoice()
        {
            if (!_speaking) return;
            _speaking = false;
            _bus.Publish(new VoiceStoppedEvent());
        }

        private void PlayOneShot(string id, AudioBus? expected)
        {
            if (TryResolve(id, expected, out var cue)) _bus.Publish(new SoundPlayedEvent(cue));
        }

        /// <summary>False for an unknown id (noted once) or a declared-silent row; the row's bus wins over the verb's.</summary>
        private bool TryResolve(string id, AudioBus? expected, out SoundCue cue)
        {
            if (!_catalog.TryGet(id, out cue))
            {
                if (id != null && _alreadyNoted.Add(id))
                {
                    Log.Info($"[Sound] '{id}' has no catalogue row yet; it stays silent.");
                    _bus.Publish(new UnknownSoundEvent(id));
                }
                return false;
            }
            if (cue.IsSilent) return false;
            if (expected.HasValue && cue.Bus != expected.Value && _alreadyNoted.Add("bus:" + id))
                Log.Warning($"[Sound] '{id}' is filed on {cue.Bus} but was played as {expected.Value}. Playing it on {cue.Bus}.");
            return true;
        }
    }
}
