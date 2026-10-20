using System;

namespace Unidad.Core.Content
{
    /// <summary>A content file that cannot be read, parsed or validated. The message starts with the file's path.</summary>
    public sealed class ContentException : Exception
    {
        public string Path { get; }

        public ContentException(string path, string message, Exception inner = null) : base($"{path}: {message}", inner) => Path = path;
    }
}
