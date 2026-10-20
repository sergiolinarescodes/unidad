using System.Collections.Generic;
using System.Linq;
using Unidad.Core.Bootstrap;

namespace Unidad.Adapter
{
    /// <summary>The boot's one-line installer list and its stub verdicts.</summary>
    internal static class InstallerOrderLog
    {
        public static string Line(IReadOnlyList<ISystemInstaller> installers) =>
            $"[Boot] {installers.Count} installers: {string.Join(", ", installers.Select(installer => installer.GetType().FullName))}";

        /// <summary>The warning naming every stub that still wins; null when none does.</summary>
        public static string StubWarning(IReadOnlyList<string> winningStubs) =>
            winningStubs.Count == 0 ? null : $"[Boot] {winningStubs.Count} stubs still win: {string.Join(", ", winningStubs)}";

        /// <summary>The <c>strict</c> failure message; null when no stub wins.</summary>
        public static string StrictFailure(IReadOnlyList<string> winningStubs) =>
            winningStubs.Count == 0 ? null : $"strict: {winningStubs.Count} stub(s) still win: {string.Join(", ", winningStubs)}.";
    }
}
