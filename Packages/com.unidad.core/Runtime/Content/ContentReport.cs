using System;
using System.Collections.Generic;

namespace Unidad.Core.Content
{
    /// <summary>The errors every <see cref="IContentCheck"/> found, so one boot names all of them at once.</summary>
    public sealed class ContentReport
    {
        public void Error(string path, string message) => throw new NotImplementedException();

        /// <summary>Each "&lt;path&gt;: &lt;message&gt;", in report order.</summary>
        public IReadOnlyList<string> Errors => throw new NotImplementedException();

        /// <summary>Throws one <see cref="ContentException"/> (the first path, "n content error(s):" and every line) when any error was reported.</summary>
        public void ThrowIfAny() => throw new NotImplementedException();

        /// <summary>Runs every check in order; a check that throws is recorded as an error under its <see cref="IContentCheck.Name"/>.</summary>
        public static ContentReport Run(IEnumerable<IContentCheck> checks) => throw new NotImplementedException();
    }
}
