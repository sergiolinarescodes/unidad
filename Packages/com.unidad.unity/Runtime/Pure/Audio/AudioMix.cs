using System;
using System.Collections.Generic;
using Unidad.Audio;

namespace Unidad.Adapter
{
    /// <summary>AudioSource volumes are linear: the cue's level times its bus times Master; muted is 0.</summary>
    internal static class BusMix
    {
        public static float Volume(float baseLinear, AudioBus bus, IReadOnlyList<float> levels, bool muted)
        {
            if (muted) return 0f;
            float level = Math.Clamp(baseLinear, 0f, 1f) * levels[(int)bus];
            return bus == AudioBus.Master ? level : level * levels[(int)AudioBus.Master];
        }
    }

    internal readonly record struct MusicFadeLevels(float Rising, float Falling, bool Done);

    /// <summary>The music hand-over: the rising track climbs to its target while the falling one drops from where it was.</summary>
    internal static class MusicFade
    {
        /// <summary>A zero-length fade is done at once.</summary>
        public static float Progress(float elapsed, float seconds) => seconds <= 0f ? 1f : Math.Clamp(elapsed / seconds, 0f, 1f);

        public static MusicFadeLevels Settle(float t, float risingTarget, float fallingFrom)
        {
            float p = Math.Clamp(t, 0f, 1f);
            return new MusicFadeLevels(risingTarget * p, fallingFrom * (1f - p), p >= 1f);
        }
    }

    /// <summary>The one-shot voices out of the pool; a sweep hands every finished one back.</summary>
    internal sealed class ActiveVoices<T> where T : class
    {
        private readonly List<T> _active = new();

        public int Count => _active.Count;

        public void Add(T voice) => _active.Add(voice ?? throw new ArgumentNullException(nameof(voice)));

        public int Sweep(Func<T, bool> stillPlaying, Action<T> release)
        {
            int released = 0;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var voice = _active[i];
                if (stillPlaying(voice)) continue;
                _active.RemoveAt(i);
                release(voice);
                released++;
            }
            return released;
        }
    }
}
