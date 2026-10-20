using System;
using System.IO;
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
        public static string SlotPath(LaunchArgs args, string defaultFolder)
        {
            var dir = args?.Get(SaveDir);
            if (dir == null) return Path.Combine(defaultFolder ?? "", FileSaveStore.SlotFile);
            if (!Path.IsPathFullyQualified(dir))
                throw new ArgumentException($"{LaunchArgs.SetFlag} {SaveDir}={dir}: the value must be an absolute folder such as C:/tmp/saves.");
            return Path.Combine(dir, FileSaveStore.SlotFile);
        }
    }
}
