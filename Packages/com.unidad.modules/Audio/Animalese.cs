using System;

namespace Unidad.Audio;

/// <summary>Every number the speech voice has.</summary>
public static class VoiceTuning
{
    public const int SampleRate = 22050;
    public const float BlipSeconds = 0.078f;
    public const float AttackSeconds = 0.005f;
    public const float ReleaseSeconds = 0.028f;
    /// <summary>Each blip starts this far above its pitch and slides down onto it: the little "wah" of animalese.</summary>
    public const float GlideSemitones = 3f;
    public const float GlideSeconds = 0.035f;
    /// <summary>How long a consonant's noisy attack lasts, at most.</summary>
    public const float ConsonantSeconds = 0.012f;
    /// <summary>A plosive's burst is this share of a fricative's.</summary>
    public const float PlosiveFraction = 0.4f;
    public const float FirstFormantQ = 4.5f;
    public const float SecondFormantQ = 6f;
    public const float SecondFormantGain = 0.55f;
    public const float DryGain = 0.12f;
    public const float PeakLevel = 0.6f;
    /// <summary>The level of the Speech player, 0..1, under the Voice bus.</summary>
    public const float VoiceVolumeLinear = 0.7f;

    public const float PlayerPitchHz = 255f;
    public const float PlayerFormantScale = 1.04f;
    public const float PlayerBreath = 0.08f;
    public const float PlayerBrightness = 0.6f;

    public const float PartnerPitchMinHz = 150f;
    public const float PartnerPitchMaxHz = 420f;
    public const float PartnerFormantMin = 0.86f;
    public const float PartnerFormantMax = 1.22f;
    public const float PartnerBreathMin = 0.03f;
    public const float PartnerBreathMax = 0.22f;
    public const float PartnerBrightnessMin = 0.3f;
    public const float PartnerBrightnessMax = 0.9f;
    /// <summary>The seed the speaker's name is hashed under.</summary>
    public const int VoiceSeed = 0x5EED_A11E;
}

/// <summary>Who is talking: the pitch their syllables centre on and the colour of their throat.</summary>
public readonly record struct VoiceProfile(float PitchHz, float FormantScale, float Breath, float Brightness);

/// <summary>One syllable to sound: a pitched, formant-filtered blip. Equal blips render equal samples.</summary>
public readonly record struct VoiceBlip(float PitchHz, float FirstFormantHz, float SecondFormantHz, float ConsonantSeconds, float Breath, float Brightness);

/// <summary>The animalese mapping: a speaker's name becomes a voice, a syllable's letters become a blip.
/// <summary>The animalese mapping: a speaker's name becomes a voice, a syllable's letters become a blip.
/// Deterministic, so the same line always sounds the same.</summary>
public static class Animalese
{
    public static VoiceProfile Player { get; } = new(VoiceTuning.PlayerPitchHz, VoiceTuning.PlayerFormantScale,
        VoiceTuning.PlayerBreath, VoiceTuning.PlayerBrightness);

    /// <summary>A voice seeded by a name: the same name always speaks the same way.</summary>
    public static VoiceProfile For(string name) => throw new NotImplementedException();

    /// <summary>The blip for a syllable: its pitch comes from its letters, its formants from its vowel.</summary>
    public static VoiceBlip Blip(VoiceProfile voice, char onset, char vowel) => throw new NotImplementedException();
}

/// <summary>Renders a blip to mono samples in -1..1. Pure: equal blips give equal samples.</summary>
public static class AnimaleseSynth
{
    public static float[] Render(VoiceBlip blip, int sampleRate = VoiceTuning.SampleRate) => throw new NotImplementedException();
}
