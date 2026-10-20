using System;

namespace Unidad.Text
{
    /// <summary>The in-code English rows of the generic keys, used for any key no text file defines.</summary>
    public static class TextDefaults
    {
        public const string Source = "(Unidad English defaults)";

        /// <summary>A new table per call (no static state).</summary>
        public static TextTable English() => throw new NotImplementedException();
    }
}
