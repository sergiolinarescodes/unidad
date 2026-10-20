using System.Collections.Generic;

namespace Unidad.Core.Content
{
    /// <summary>
    /// Where content JSON is read from. A path is "&lt;Folder&gt;/&lt;file&gt;.json" relative to the content root, with forward
    /// slashes; the folder may itself contain slashes ("Characters/dialogue"). The adapter roots it at
    /// <c>Application.streamingAssetsPath/Content</c>.
    /// </summary>
    public interface IContentSource
    {
        /// <summary>The file's text; throws <see cref="ContentException"/> when it cannot be read.</summary>
        string Read(string path);

        bool Exists(string path);

        /// <summary>
        /// Files in <paramref name="folder"/> (a folder path, or "*" for every first-level folder under the root) whose file name
        /// matches <paramref name="pattern"/> ('*' any run, '?' one char, case-sensitive), as content paths, sorted ordinal,
        /// without duplicates. Not recursive.
        /// </summary>
        IReadOnlyList<string> List(string folder, string pattern);
    }
}
