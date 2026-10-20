namespace Unidad.Audio
{
    /// <summary>A catalogue row. Stream is an AssetLibrary key the adapter loads; null or empty is declared silence.</summary>
    public readonly record struct SoundCue(string Id, string Stream, AudioBus Bus, float BaseVolumeLinear)
    {
        public const float FullVolume = 1f;

        public bool IsSilent => string.IsNullOrEmpty(Stream);
    }

    /// <summary>The content row of sounds.json / sounds.*.json (a JSON array of these).</summary>
    public sealed record SoundRow
    {
        public required string Id { get; init; }
        public string Stream { get; init; }
        public AudioBus Bus { get; init; } = AudioBus.Sfx;
        public float Volume { get; init; } = 1f;
    }
}
