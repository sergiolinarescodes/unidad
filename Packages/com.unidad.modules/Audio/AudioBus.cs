using System;
using System.Collections.Generic;

namespace Unidad.Audio
{
    /// <summary>
    /// The buses the game mixes through. Master is the parent; the others are its children, one per kind of sound a player
    /// could set a level for. Voice carries the dialogue's animalese. The values are frozen.
    /// </summary>
    public enum AudioBus
    {
        Master = 0,
        Music = 1,
        Sfx = 2,
        Ui = 3,
        Voice = 4,
    }

    /// <summary>The bus names, engine-free: the same strings name a bus in content and in a test.</summary>
    public static class AudioBuses
    {
        public const string Master = "Master", Music = "Music", Sfx = "Sfx", Ui = "Ui", Voice = "Voice";

        /// <summary>Master first: a child bus exists only once its parent does.</summary>
        public static IReadOnlyList<AudioBus> All { get; } = new[] { AudioBus.Master, AudioBus.Music, AudioBus.Sfx, AudioBus.Ui, AudioBus.Voice };

        public static string NameOf(AudioBus bus) => throw new NotImplementedException();
    }
}
