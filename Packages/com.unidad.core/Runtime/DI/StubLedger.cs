using System;
using System.Collections.Generic;

namespace Unidad.Core.DI
{
    /// <summary>The stubs still serving at boot, such as <c>IDialogueAction:spawn_cards</c>, in record order; duplicates are ignored.</summary>
    public sealed class StubLedger
    {
        private readonly List<string> _entries = new();
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public IReadOnlyList<string> Entries => _entries;

        public void Record(string what)
        {
            if (string.IsNullOrWhiteSpace(what)) throw new ArgumentException("A stub entry needs a name.", nameof(what));
            if (_seen.Add(what)) _entries.Add(what);
        }
    }
}
