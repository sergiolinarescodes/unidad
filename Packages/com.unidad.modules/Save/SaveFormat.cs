using System;

namespace Unidad.Save
{
    /// <summary>The save document: <c>{ "version": 1, "sections": { "&lt;participant key&gt;": &lt;any JSON&gt; } }</c>.</summary>
    public static class SaveFormat
    {
        public const int CurrentVersion = 1;
        public const string VersionField = "version", SectionsField = "sections";
    }

    /// <summary>A section a participant cannot read; the load becomes Unreadable and nothing is applied.</summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message)
        {
        }
    }
}
