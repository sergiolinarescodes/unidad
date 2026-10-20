using System;
using System.Collections.Generic;
using Unidad.Core.DI;

namespace Unidad.Core.Registry
{
    /// <summary>Content-named handlers (one per id) folded from a multi-binding, where a real handler replaces a stub.</summary>
    public static class HandlerRegistry
    {
        /// <summary>
        /// <paramref name="handlers"/> in container order, keyed by <paramref name="id"/>: a non-stub replaces an <see cref="IStub"/>
        /// with the same id, a stub never replaces a non-stub, two non-stubs with one id throw. Every id still served by a stub is
        /// recorded in <paramref name="ledger"/> as "&lt;what&gt;:&lt;id&gt;"; <paramref name="what"/> defaults to the handler type's name.
        /// </summary>
        public static OrderedRegistry<T> ById<T>(IEnumerable<T> handlers, Func<T, string> id, StubLedger ledger = null, string what = null)
            where T : class
            => throw new NotImplementedException();
    }
}
