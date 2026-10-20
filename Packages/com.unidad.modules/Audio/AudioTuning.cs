namespace Unidad.Audio;

/// <summary>Every number the sound system has.</summary>
public static class AudioTuning
{
    /// <summary>The decibel value that stands for silence: the mixer floor is -80 dB.</summary>
    public const float SilenceDecibels = -80f;

    /// <summary>A linear level at or under this reads as OFF; below it the dB curve runs away to minus infinity.</summary>
    public const float SilenceThresholdLinear = 0.0005f;

    /// <summary>How long one track takes to hand over to the next.</summary>
    public const float MusicCrossfadeSeconds = 1.2f;

    /// <summary>How long <see cref="ISoundService.StopMusic"/> takes to fade out; nothing arrives to cover the tail.</summary>
    public const float MusicFadeOutSeconds = 0.6f;

    /// <summary>How many one-shot voices the adapter keeps pooled; past this the pool grows and warns.</summary>
    public const int OneShotVoices = 16;
}

/// <summary>The launch key Audio reads.</summary>
public static class AudioSettings
{
    public const string Mute = "mute";
}
