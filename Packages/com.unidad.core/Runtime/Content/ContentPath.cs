using System;

namespace Unidad.Core.Content
{
    /// <summary>The grammar of content paths and file patterns, identical for every <see cref="IContentSource"/>.</summary>
    public static class ContentPath
    {
        /// <summary>The folder separator of every content path, whatever the platform.</summary>
        public const char Separator = '/';

        /// <summary>
        /// Refuses anything but a relative "Folder/file.json": a rooted path, a backslash, an empty, "." or ".." segment throws
        /// <see cref="ContentException"/> naming the path.
        /// </summary>
        public static void Validate(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0 || path.IndexOf(Separator) < 0)
                throw new ContentException(path ?? "(null)", "a content path is Folder/file.json.");
            foreach (var segment in path.Split(Separator))
                if (segment.Length == 0 || segment == "." || segment == "..")
                    throw new ContentException(path, "a content path is Folder/file.json.");
        }

        /// <summary>True when <paramref name="name"/> matches <paramref name="pattern"/> whole: '*' any run (dots included), '?' one char, ordinal.</summary>
        public static bool Matches(string name, string pattern)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            int n = 0, p = 0, star = -1, mark = 0;
            while (n < name.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == name[n])) { n++; p++; }
                else if (p < pattern.Length && pattern[p] == '*') { star = p++; mark = n; }
                else if (star >= 0) { p = star + 1; n = ++mark; }
                else return false;
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }

        /// <summary>The layer a layered path names: "mymod:Cards/x.json" gives "mymod"; a base path gives null.</summary>
        public static string LayerOf(string path) => throw new NotImplementedException();
    }
}
