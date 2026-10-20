using System;

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
        public FileSaveStore(string slotPath) => throw new NotImplementedException();

        /// <summary>The full path of the slot file.</summary>
        public string Location => throw new NotImplementedException();

        public string Read() => throw new NotImplementedException();

        public void Write(string text) => throw new NotImplementedException();
    }
}
