using System;
using System.Collections.Generic;
using Unidad.Core.Content;

namespace Unidad.Text
{
    /// <summary>Loads the merged text table of a locale from every "text.*.&lt;locale&gt;.json" under the content root.</summary>
    public static class TextContent
    {
        public const string DefaultLocale = "en";

        /// <summary>"text.*." + locale + ".json".</summary>
        public static string PatternFor(string locale) => throw new NotImplementedException();

        /// <summary>
        /// The merged table for <paramref name="locale"/>, falling back row by row to English and then to <see cref="TextDefaults"/>;
        /// a key a <see cref="TextKeySet"/> needs and no file has is an error naming the files and the keys.
        /// </summary>
        public static TextTable Load(IContentSource source, IReadOnlyList<TextKeySet> sets, string locale = DefaultLocale)
            => throw new NotImplementedException();
    }
}
