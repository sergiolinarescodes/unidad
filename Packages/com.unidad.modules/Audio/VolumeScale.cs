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
        public static float ToDecibels(float linear, bool muted = false) => throw new NotImplementedException();

        public static bool IsSilent(float linear, bool muted = false) => throw new NotImplementedException();

        public static float Clamp01(float value) => throw new NotImplementedException();
    }
}
