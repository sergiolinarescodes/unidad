using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Unidad.Core.Content
{
    /// <summary>Content files under a folder on disk (tools, the sidecar, and the adapter over StreamingAssets/Content).</summary>
    public sealed class DirectoryContentSource : IContentSource
    {
        public DirectoryContentSource(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("A content root is a folder path.", nameof(root));
            Root = root;
        }

        public string Root { get; }

        public string Read(string path)
        {
            var file = FullPath(path);
            if (!File.Exists(file)) throw new ContentException(path, "does not exist.");
            try
            {
                return File.ReadAllText(file, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new ContentException(path, $"cannot be read ({ex.Message}).", ex);
            }
        }

        public bool Exists(string path) => File.Exists(FullPath(path));

        public IReadOnlyList<string> List(string folder, string pattern)
        {
            if (folder == null) throw new ArgumentNullException(nameof(folder));
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            var found = new List<string>();
            if (!Directory.Exists(Root)) return found;
            if (folder == "*")
            {
                foreach (var directory in Directory.GetDirectories(Root))
                    AddMatches(found, Path.GetFileName(directory), directory, pattern);
            }
            else
            {
                var directory = FullPath(folder);
                if (Directory.Exists(directory)) AddMatches(found, folder, directory, pattern);
            }
            found.Sort(StringComparer.Ordinal);
            return found;
        }

        private static void AddMatches(List<string> found, string folder, string directory, string pattern)
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                var name = Path.GetFileName(file);
                if (ContentPath.Matches(name, pattern)) found.Add(folder + ContentPath.Separator + name);
            }
        }

        // Trust boundary (mods): a path never leaves the root.
        private string FullPath(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.IndexOf(':') >= 0)
                throw new ContentException(path ?? "(null)", "is outside the content root.");
            foreach (var segment in path.Split(ContentPath.Separator, '\\'))
                if (segment == "..")
                    throw new ContentException(path, "is outside the content root.");
            return Path.Combine(Root, path.Replace(ContentPath.Separator, Path.DirectorySeparatorChar));
        }
    }
}
