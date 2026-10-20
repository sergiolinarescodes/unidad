namespace Unidad.Audio
{
    /// <summary>A one-shot's catalogue key. Wrapped so a music id can never reach a one-shot verb.</summary>
    public readonly record struct SoundId(string Value)
    {
        public override string ToString() => Value;
    }

    /// <summary>A music track's catalogue key.</summary>
    public readonly record struct MusicId(string Value)
    {
        public override string ToString() => Value;
    }

    /// <summary>The cues the framework's own screens play; each stays silent until a catalogue row gives it a stream.</summary>
    public static class UiCues
    {
        public static SoundId PauseOpen { get; } = new("ui.pause.open");
        public static SoundId PauseClose { get; } = new("ui.pause.close");
        public static SoundId Click { get; } = new("ui.click");
        public static SoundId Hover { get; } = new("ui.hover");
    }
}
