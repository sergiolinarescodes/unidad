using System.Collections.Generic;

namespace Unidad.Text
{
    /// <summary>The nine keys the framework's own screens read.</summary>
    public static class TextKeys
    {
        public const string PauseTitle = "pause.title", PauseResume = "pause.resume", PauseQuit = "pause.quit",
            QuitTitle = "quit.title", QuitStay = "quit.stay", QuitConfirm = "quit.confirm",
            BootFailed = "boot.failed", SaveRefused = "save.refused" /* {0} = save version */, SaveUnreadable = "save.unreadable";

        /// <summary>The nine generic keys, in declaration order.</summary>
        public static IReadOnlyList<string> Generic { get; } = new[]
        {
            PauseTitle, PauseResume, PauseQuit, QuitTitle, QuitStay, QuitConfirm, BootFailed, SaveRefused, SaveUnreadable,
        };
    }
}
