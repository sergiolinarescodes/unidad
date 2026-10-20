using System;
using System.IO;
using System.Text;

namespace Unidad.Save
{
    /// <summary>
    /// The slot as a file: writes "&lt;slot&gt;.tmp", then replaces the slot keeping the old one as "&lt;slot&gt;.bak", so a
    /// crash mid-write leaves the old slot.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        public const string SlotFile = "slot0.json", TempSuffix = ".tmp", BackupSuffix = ".bak";

        /// <param name="slotPath">The full path of the slot file.</param>
        public FileSaveStore(string slotPath)
        {
            if (string.IsNullOrWhiteSpace(slotPath)) throw new ArgumentException("A save slot needs a path.", nameof(slotPath));
            Location = slotPath;
        }

        /// <summary>The full path of the slot file.</summary>
        public string Location { get; }

        public string Read()
        {
            if (!File.Exists(Location)) return null;
            try
            {
                return File.ReadAllText(Location);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"cannot open it ({ex.Message}).", ex);
            }
        }

        public void Write(string text)
        {
            string temp = Location + TempSuffix;
            try
            {
                var folder = Path.GetDirectoryName(Location);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(temp, text ?? "", new UTF8Encoding(false));
                if (File.Exists(Location)) File.Replace(temp, Location, Location + BackupSuffix);
                else File.Move(temp, Location);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException($"cannot write it ({ex.Message}).", ex);
            }
        }
    }
}
