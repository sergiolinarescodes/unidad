using System;

namespace Unidad.Adapter
{
    /// <summary>The one quit call: in the editor, batch mode exits with the code and a play session stops; a player quits with it.</summary>
    internal static class AppQuit
    {
        public static void Now(int code) => throw new NotImplementedException();
    }
}
