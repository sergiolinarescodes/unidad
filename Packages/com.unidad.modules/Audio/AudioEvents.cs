namespace Unidad.Audio;

/// <summary>A one-shot was asked for. The adapter takes a pooled voice and plays it.</summary>
public readonly record struct SoundPlayedEvent(SoundCue Cue);

/// <summary>A track was asked for; <paramref name="CrossfadeSeconds"/> is zero when nothing was playing.</summary>
public readonly record struct MusicStartedEvent(SoundCue Cue, float CrossfadeSeconds);

/// <summary>The music channel was asked to go quiet over <paramref name="FadeSeconds"/>.</summary>
public readonly record struct MusicStoppedEvent(float FadeSeconds);

/// <summary>A syllable of speech was asked for on the Voice bus; it cuts the previous one.</summary>
public readonly record struct VoiceBlipEvent(VoiceBlip Blip);

/// <summary>The speech voice was asked to go quiet now.</summary>
public readonly record struct VoiceStoppedEvent;
