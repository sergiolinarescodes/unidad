using System;

namespace Unidad.Audio
{
    /// <summary>
    /// The ONE place a 0..1 level becomes a bus level in decibels, so the mute rule and the silence
    /// floor cannot drift between call sites.
    /// </summary>
    public static class VolumeScale
    {
        /// <summary>1 is 0 dB; a level at or under the silence threshold, or anything while muted, is silence.</summary>
        public static float ToDecibels(float linear, bool muted = false)
        {
            if (muted) return AudioTuning.SilenceDecibels;
            var clamped = Clamp01(linear);
            if (clamped <= AudioTuning.SilenceThresholdLinear) return AudioTuning.SilenceDecibels;
            var decibels = (float)(20.0 * Math.Log10(clamped));
            return decibels < AudioTuning.SilenceDecibels ? AudioTuning.SilenceDecibels : decibels;
        }

        public static bool IsSilent(float linear, bool muted = false) => muted || Clamp01(linear) <= AudioTuning.SilenceThresholdLinear;

        public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
