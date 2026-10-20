using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>The one quit call: in the editor, batch mode exits with the code and a play session stops; a player quits with it.</summary>
    internal static class AppQuit
    {
        public static void Now(int code)
        {
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(code);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }
    }

    /// <summary>The bound <see cref="IAppQuit"/>: a report writer sets the code, the flow or <c>frames</c> quits with it.</summary>
    internal sealed class AppQuitService : IAppQuit
    {
        public int ExitCode { get; private set; }

        public void SetExitCode(int code) => ExitCode = code;

        public void Quit() => AppQuit.Now(ExitCode);
    }
}
