using System;
using System.Collections.Generic;

namespace Unidad.Text
{
    /// <summary>
    /// One locale's key-to-template rows. Keys are dot-separated segments of ASCII letters, digits, '-' and '_'; templates use
    /// {0}..{9}, and named markup such as {name} is exempt from the format check.
    /// </summary>
    public sealed class TextTable
    {
        public const int MaxTemplateArguments = 10;

        /// <summary>No rows; Source "(no text table)".</summary>
        public static TextTable Empty => throw new NotImplementedException();

        public string Source => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public IReadOnlyCollection<string> Keys => throw new NotImplementedException();

        public bool TryGet(string key, out string text) => throw new NotImplementedException();

        /// <summary>One JSON object of key to template; a FormatException starting "&lt;source&gt;: " on bad input.</summary>
        public static TextTable Parse(string json, string source) => throw new NotImplementedException();

        /// <summary>A key in two tables is a FormatException naming both sources.</summary>
        public static TextTable Merge(IReadOnlyList<TextTable> tables) => throw new NotImplementedException();

        /// <summary>A new table with these rows plus the rows of <paramref name="fallback"/> whose key this table lacks.</summary>
        public TextTable WithFallback(TextTable fallback) => throw new NotImplementedException();

        public static bool IsValidKey(string key) => throw new NotImplementedException();
    }
}
