using System;
using Unidad.Launch;

namespace Unidad.Save
{
    /// <summary>The launch keys Save reads, and where the slot lives.</summary>
    public static class SaveSettings
    {
        public const string SaveDir = "save.dir", Fresh = "fresh";

        /// <summary>
        /// The full path of slot0.json: under <c>save.dir</c> when given (it must be fully qualified, else an ArgumentException
        /// naming it), else under <paramref name="defaultFolder"/>.
        /// </summary>
        public static string SlotPath(LaunchArgs args, string defaultFolder) => throw new NotImplementedException();
    }
}
