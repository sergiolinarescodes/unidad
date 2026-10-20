using System;
using System.Collections.Generic;
using System.Globalization;
using Unidad.Core.Diagnostics;

namespace Unidad.Text
{
    /// <summary>Resolves keys against one <see cref="TextTable"/>; a miss is logged once per key.</summary>
    internal sealed class TextService : ITextService
    {
        private readonly TextTable _table;
        private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

        public TextService(TextTable table) => _table = table ?? TextTable.Empty;

        /// <summary>What a missing key shows: the key itself, bracketed, so it is found on sight.</summary>
        public static string Missing(string key) => $"[{key}]";

        public bool Has(string key) => _table.TryGet(key, out _);

        public string Get(string key) => Format(key);

        public string Format(string key, params object[] args)
        {
            if (!_table.TryGet(key, out var template))
            {
                if (_reported.Add(key ?? "")) Log.Warning($"[Text] {_table.Source} has no '{key}'; showing {Missing(key ?? "")}.");
                return Missing(key ?? "");
            }
            if (template.IndexOfAny(new[] { '{', '}' }) < 0) return template;
            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args ?? Array.Empty<object>());
            }
            catch (FormatException)
            {
                if (_reported.Add("args:" + key))
                    Log.Warning($"[Text] '{key}' needs more than the {args?.Length ?? 0} argument(s) it was given; showing the template.");
                return template;
            }
        }
    }
}
