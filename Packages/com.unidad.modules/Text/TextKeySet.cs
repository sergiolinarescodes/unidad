using System.Collections.Generic;

namespace Unidad.Text
{
    /// <summary>The keys one task's text file must define; added with <c>AddMany&lt;TextKeySet&gt;</c>. Module is a label ("Cards.cards").</summary>
    public sealed record TextKeySet(string Module, IReadOnlyList<string> Keys);
}
