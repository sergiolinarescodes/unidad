using System;
using System.Collections.Generic;
using System.Linq;

namespace Unidad.Core.Content
{
    /// <summary>Content held in memory, keyed by content path: smoke tests feed inline JSON through it, with the file system's rules.</summary>
    public sealed class MemoryContentSource : IContentSource
    {
        private readonly Dictionary<string, string> _files;

        public MemoryContentSource(IReadOnlyDictionary<string, string> files)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            _files = files.ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
        }

        public string Read(string path)
        {
            if (path != null && _files.TryGetValue(path, out var text)) return text;
            throw new ContentException(path ?? "(null)", "does not exist.");
        }

        public bool Exists(string path) => path != null && _files.ContainsKey(path);

        public IReadOnlyList<string> List(string folder, string pattern)
        {
            if (folder == null) throw new ArgumentNullException(nameof(folder));
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            var found = new List<string>();
            foreach (var path in _files.Keys)
            {
                var slash = path.LastIndexOf(ContentPath.Separator);
                if (slash <= 0) continue;
                var directory = path.Substring(0, slash);
                var inFolder = folder == "*" ? directory.IndexOf(ContentPath.Separator) < 0 : directory == folder;
                if (inFolder && ContentPath.Matches(path.Substring(slash + 1), pattern)) found.Add(path);
            }
            found.Sort(StringComparer.Ordinal);
            return found;
        }
    }
}
