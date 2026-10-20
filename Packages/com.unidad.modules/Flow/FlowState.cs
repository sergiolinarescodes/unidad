namespace Unidad.Flow
{
    /// <summary>The game's top-level state. The values are frozen.</summary>
    public enum FlowState
    {
        Boot = 0,
        Loading = 1,
        Play = 2,
        Paused = 3,
        QuitConfirm = 4,
        Quit = 5,
    }
}
