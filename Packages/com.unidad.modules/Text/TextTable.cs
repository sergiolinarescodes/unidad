using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unidad.Text
{
    /// <summary>
    /// One locale's key-to-template rows. Keys are dot-separated segments of ASCII letters, digits, '-' and '_'; templates use
    /// {0}..{9}, and named markup such as {name} is exempt from the format check.
    /// </summary>
    public sealed class TextTable
    {
        public const int MaxTemplateArguments = 10;

        // Named markup ({damage}, {key.poison}) is filled by description builders, not string.Format; a doubled {{x}} is an escape and stays checked.
        private static readonly Regex Markup = new(@"(?<!\{)\{[A-Za-z][A-Za-z0-9_.]*\}(?!\})");

        private readonly Dictionary<string, string> _rows;

        private TextTable(Dictionary<string, string> rows, string source)
        {
            _rows = rows;
            Source = source;
        }

        /// <summary>No rows; Source "(no text table)".</summary>
        public static TextTable Empty => new(new Dictionary<string, string>(StringComparer.Ordinal), "(no text table)");

        public string Source { get; }

        public int Count => _rows.Count;

        public IReadOnlyCollection<string> Keys => _rows.Keys;

        public bool TryGet(string key, out string text)
        {
            if (key != null && _rows.TryGetValue(key, out var found))
            {
                text = found;
                return true;
            }
            text = "";
            return false;
        }

        /// <summary>One JSON object of key to template; a FormatException starting "&lt;source&gt;: " on bad input.</summary>
        public static TextTable Parse(string json, string source)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json ?? "", new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException ex)
            {
                throw new FormatException($"{source}: not valid JSON ({ex.Message}).", ex);
            }
            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new FormatException($"{source}: a text table is one JSON object of key -> text.");
                var rows = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    var key = property.Name;
                    if (!IsValidKey(key))
                        throw new FormatException($"{source}: '{key}' is not a key (dot-separated segments of letters, digits, '-' or '_').");
                    if (property.Value.ValueKind != JsonValueKind.String)
                        throw new FormatException($"{source}: '{key}' must be a string.");
                    var text = property.Value.GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(text)) throw new FormatException($"{source}: '{key}' is blank.");
                    if (!IsValidTemplate(text)) throw new FormatException($"{source}: '{key}' has braces that do not format: {text}");
                    if (!rows.TryAdd(key, text)) throw new FormatException($"{source}: '{key}' appears twice.");
                }
                return new TextTable(rows, source);
            }
        }

        /// <summary>A key in two tables is a FormatException naming both sources.</summary>
        public static TextTable Merge(IReadOnlyList<TextTable> tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (tables.Count == 1) return tables[0];
            var rows = new Dictionary<string, string>(StringComparer.Ordinal);
            var origin = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var table in tables)
                foreach (var row in table._rows)
                {
                    if (origin.TryGetValue(row.Key, out var first))
                        throw new FormatException($"{table.Source}: '{row.Key}' is already defined in {first}.");
                    origin.Add(row.Key, table.Source);
                    rows.Add(row.Key, row.Value);
                }
            string source = tables.Count == 0 ? Empty.Source : $"{tables[0].Source} (+{tables.Count - 1} module tables)";
            return new TextTable(rows, source);
        }

        /// <summary>A new table with these rows plus the rows of <paramref name="fallback"/> whose key this table lacks.</summary>
        public TextTable WithFallback(TextTable fallback)
        {
            if (fallback == null) throw new ArgumentNullException(nameof(fallback));
            var rows = new Dictionary<string, string>(_rows, StringComparer.Ordinal);
            foreach (var row in fallback._rows) rows.TryAdd(row.Key, row.Value);
            return new TextTable(rows, Source);
        }

        public static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            foreach (var segment in key.Split('.'))
            {
                if (segment.Length == 0) return false;
                foreach (char c in segment)
                    if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')) return false;
            }
            return true;
        }

        private static bool IsValidTemplate(string text)
        {
            text = Markup.Replace(text, "");
            if (text.IndexOfAny(new[] { '{', '}' }) < 0) return true;
            try
            {
                string.Format(CultureInfo.InvariantCulture, text, new object[MaxTemplateArguments]);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
