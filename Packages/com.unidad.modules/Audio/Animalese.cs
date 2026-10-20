using System;
using Unidad.Core.Random;

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
/// Deterministic, so the same line always sounds the same.</summary>
public static class Animalese
{
    /// <summary>Semitone steps a syllable can sit on above or below the voice's pitch (a loose pentatonic).</summary>
    private static ReadOnlySpan<int> Steps => new[] { 0, 2, 4, 7, -3, 5, -5, 9, 3, -2 };

    public static VoiceProfile Player { get; } = new(VoiceTuning.PlayerPitchHz, VoiceTuning.PlayerFormantScale,
        VoiceTuning.PlayerBreath, VoiceTuning.PlayerBrightness);

    /// <summary>A voice seeded by a name: the same name always speaks the same way.</summary>
    public static VoiceProfile For(string name)
    {
        int seed = SeedMix.Derive(VoiceTuning.VoiceSeed, StableHash(name));
        // Pitch is spread on a log scale so low and high voices are equally likely.
        float pitch = VoiceTuning.PartnerPitchMinHz * MathF.Pow(VoiceTuning.PartnerPitchMaxHz / VoiceTuning.PartnerPitchMinHz, Unit(seed, 1));
        return new VoiceProfile(pitch,
            Lerp(VoiceTuning.PartnerFormantMin, VoiceTuning.PartnerFormantMax, Unit(seed, 2)),
            Lerp(VoiceTuning.PartnerBreathMin, VoiceTuning.PartnerBreathMax, Unit(seed, 3)),
            Lerp(VoiceTuning.PartnerBrightnessMin, VoiceTuning.PartnerBrightnessMax, Unit(seed, 4)));
    }

    /// <summary>The blip for a syllable: its pitch comes from its letters, its formants from its vowel.</summary>
    public static VoiceBlip Blip(VoiceProfile voice, char onset, char vowel)
    {
        int step = Steps[(Letter(vowel) * 7 + Letter(onset) * 3) % Steps.Length];
        var (f1, f2) = Formants(vowel);
        return new VoiceBlip(voice.PitchHz * MathF.Pow(2, step / 12f), f1 * voice.FormantScale, f2 * voice.FormantScale,
            Hissy(onset) ? VoiceTuning.ConsonantSeconds : Plosive(onset) ? VoiceTuning.ConsonantSeconds * VoiceTuning.PlosiveFraction : 0,
            voice.Breath, voice.Brightness);
    }

    /// <summary>The first two formants (Hz) of an adult vowel; a vowelless syllable is a neutral schwa.</summary>
    private static (float, float) Formants(char vowel) => vowel switch
    {
        'a' => (800, 1250),
        'e' => (500, 1900),
        'i' => (320, 2300),
        'o' => (520, 900),
        'u' => (340, 800),
        'y' => (360, 2100),
        _ => (520, 1500),
    };

    private static bool Hissy(char c) => c is 's' or 'f' or 'h' or 'z' or 'c' or 'x' or 'j' or 'v';

    private static bool Plosive(char c) => c is 'p' or 't' or 'k' or 'b' or 'd' or 'g' or 'q';

    private static int Letter(char c) => c is >= 'a' and <= 'z' ? c - 'a' + 1 : c == '\0' ? 0 : c % 29;

    /// <summary>FNV-1a over the UTF-16 units: unlike <c>string.GetHashCode</c>, the same in every run.</summary>
    internal static int StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text ?? "") hash = (hash ^ c) * 16777619;
            return (int)hash;
        }
    }

    private static float Unit(int seed, int salt) => (uint)SeedMix.Derive(seed, salt) / (float)uint.MaxValue;

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}

/// <summary>Renders a blip to mono samples in -1..1: a gliding glottal buzz through two vowel formants, with a consonant's
/// noisy attack and the speaker's breath. Pure: equal blips give equal samples.</summary>
public static class AnimaleseSynth
{
    public static float[] Render(VoiceBlip blip, int sampleRate = VoiceTuning.SampleRate)
    {
        int count = (int)(VoiceTuning.BlipSeconds * sampleRate);
        var samples = new float[count];
        var first = new Resonator(blip.FirstFormantHz, VoiceTuning.FirstFormantQ, sampleRate);
        var second = new Resonator(blip.SecondFormantHz, VoiceTuning.SecondFormantQ, sampleRate);
        uint noise = (uint)SeedMix.Derive(VoiceTuning.VoiceSeed, (int)(blip.PitchHz * 64)) | 1u;
        double phase = 0;
        float peak = 0;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float glide = VoiceTuning.GlideSemitones * MathF.Max(0, 1 - t / VoiceTuning.GlideSeconds);
            phase += blip.PitchHz * MathF.Pow(2, glide / 12f) / sampleRate;
            phase -= Math.Floor(phase);
            float saw = (float)(2 * phase - 1);
            float sine = MathF.Sin((float)(2 * Math.PI * phase));
            float source = sine + (saw - sine) * blip.Brightness;
            noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
            float hiss = noise / (float)uint.MaxValue * 2 - 1;
            source += hiss * blip.Breath;
            if (t < blip.ConsonantSeconds) source = source * (t / blip.ConsonantSeconds) + hiss * (1 - t / blip.ConsonantSeconds);
            float voiced = first.Next(source) + second.Next(source) * VoiceTuning.SecondFormantGain + source * VoiceTuning.DryGain;
            float attack = MathF.Min(1, t / VoiceTuning.AttackSeconds);
            float release = MathF.Min(1, (count - i) / (VoiceTuning.ReleaseSeconds * sampleRate));
            samples[i] = voiced * attack * release * release;
            peak = MathF.Max(peak, MathF.Abs(samples[i]));
        }
        if (peak > 0)
            for (int i = 0; i < count; i++) samples[i] *= VoiceTuning.PeakLevel / peak;
        return samples;
    }

    /// <summary>An RBJ band-pass biquad (0 dB peak): one formant.</summary>
    private struct Resonator
    {
        private readonly float _b0, _b2, _a1, _a2;
        private float _x1, _x2, _y1, _y2;

        public Resonator(float hz, float q, int sampleRate)
        {
            float w = 2 * MathF.PI * MathF.Min(hz, sampleRate * 0.45f) / sampleRate;
            float alpha = MathF.Sin(w) / (2 * q);
            float a0 = 1 + alpha;
            _b0 = alpha / a0;
            _b2 = -alpha / a0;
            _a1 = -2 * MathF.Cos(w) / a0;
            _a2 = (1 - alpha) / a0;
            _x1 = _x2 = _y1 = _y2 = 0;
        }

        public float Next(float x)
        {
            float y = _b0 * x + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
            return y;
        }
    }
}
