namespace Unidad.Save
{
    /// <summary>How a load ended. The values are frozen.</summary>
    public enum SaveLoadOutcome
    {
        NoSave = 0,
        Loaded = 1,
        RefusedFutureVersion = 2,
        Unreadable = 3,
    }

    public readonly record struct SaveLoadResult(SaveLoadOutcome Outcome, int Version, string Message);

    public readonly record struct GameSavedEvent(int Version);

    public readonly record struct GameLoadedEvent(SaveLoadResult Result);
}
