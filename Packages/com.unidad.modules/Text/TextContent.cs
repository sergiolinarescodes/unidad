using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unidad.Core.Content;
using Unidad.Core.Diagnostics;

namespace Unidad.Text
{
    /// <summary>Loads the merged text table of a locale from every "text.*.&lt;locale&gt;.json" under the content root.</summary>
    public static class TextContent
    {
        public const string DefaultLocale = "en";

        private static readonly Regex LocaleShape = new(@"^[a-z]{2}(-[A-Z]{2})?$");

        /// <summary>"text.*." + locale + ".json".</summary>
        public static string PatternFor(string locale) => "text.*." + locale + ".json";

        /// <summary>
        /// The merged table for <paramref name="locale"/>, falling back row by row to English and then to <see cref="TextDefaults"/>;
        /// a key a <see cref="TextKeySet"/> needs and no file has is an error naming the files and the keys.
        /// </summary>
        public static TextTable Load(IContentSource source, IReadOnlyList<TextKeySet> sets, string locale = DefaultLocale)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (locale == null || !LocaleShape.IsMatch(locale))
                throw new ContentException("text", $"locale '{locale}' is not a locale such as en or es.");
            var englishPaths = source.List("*", PatternFor(DefaultLocale));
            var english = ParseAll(source, englishPaths);
            var paths = englishPaths;
            var table = english;
            if (locale != DefaultLocale)
            {
                paths = source.List("*", PatternFor(locale));
                if (paths.Count == 0) throw new ContentException("text", $"no text file for locale '{locale}' ({PatternFor(locale)}).");
                var local = ParseAll(source, paths);
                int fallen = english.Keys.Count(key => !local.TryGet(key, out _));
                if (fallen > 0) Log.Info($"[Text] {locale}: {fallen} row(s) fall back to English.");
                table = local.WithFallback(english);
            }
            table = table.WithFallback(TextDefaults.English());

            var missing = new List<string>();
            var absent = TextKeys.Generic.Where(key => !table.TryGet(key, out _)).ToArray();
            if (absent.Length > 0) missing.Add($"{string.Join(", ", absent)} (generic)");
            foreach (var set in sets ?? Array.Empty<TextKeySet>())
            {
                absent = set.Keys.Where(key => !table.TryGet(key, out _)).ToArray();
                if (absent.Length > 0) missing.Add($"{string.Join(", ", absent)} ({set.Module})");
            }
            if (missing.Count > 0)
                throw new FormatException($"The text table ({string.Join(", ", paths)}) has no row for {string.Join("; ", missing)}.");
            return table;
        }

        private static TextTable ParseAll(IContentSource source, IReadOnlyList<string> paths) =>
            paths.Count == 0 ? TextTable.Empty : TextTable.Merge(paths.Select(path => TextTable.Parse(source.Read(path), path)).ToList());
    }
}
