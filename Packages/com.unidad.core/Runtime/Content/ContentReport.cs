using System;
using System.Collections.Generic;

namespace Unidad.Core.Content
{
    /// <summary>The errors every <see cref="IContentCheck"/> found, so one boot names all of them at once.</summary>
    public sealed class ContentReport
    {
        private readonly List<string> _errors = new();
        private string _firstPath;

        public void Error(string path, string message)
        {
            _firstPath ??= path;
            _errors.Add($"{path}: {message}");
        }

        /// <summary>Each "&lt;path&gt;: &lt;message&gt;", in report order.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>Throws one <see cref="ContentException"/> (the first path, "n content error(s):" and every line) when any error was reported.</summary>
        public void ThrowIfAny()
        {
            if (_errors.Count == 0) return;
            throw new ContentException(_firstPath, $"{_errors.Count} content error(s):\n" + string.Join("\n", _errors));
        }

        /// <summary>Runs every check in order; a check that throws is recorded as an error under its <see cref="IContentCheck.Name"/>.</summary>
        public static ContentReport Run(IEnumerable<IContentCheck> checks)
        {
            if (checks == null) throw new ArgumentNullException(nameof(checks));
            var report = new ContentReport();
            foreach (var check in checks)
            {
                try
                {
                    check.Check(report);
                }
                catch (Exception ex)
                {
                    report.Error(check.Name, ex.Message);
                }
            }
            return report;
        }
    }
}
