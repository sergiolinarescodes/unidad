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
            => Fold(handlers, id, ledger, what ?? typeof(T).Name);

        internal static OrderedRegistry<T> Fold<T>(IEnumerable<T> handlers, Func<T, string> idOf, StubLedger ledger, string what)
        {
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));
            if (idOf == null) throw new ArgumentNullException(nameof(idOf));
            var registry = new OrderedRegistry<T>();
            foreach (var handler in handlers)
            {
                var id = idOf(handler);
                if (string.IsNullOrWhiteSpace(id))
                    throw new InvalidOperationException($"{typeof(T).Name} '{handler?.GetType().FullName}' has an empty id.");
                if (!registry.TryGet(id, out var existing))
                {
                    registry.Add(id, handler);
                    continue;
                }
                bool existingStub = existing is IStub, newStub = handler is IStub;
                if (newStub && !existingStub) continue;
                if (!newStub && !existingStub)
                    throw new InvalidOperationException(
                        $"{typeof(T).Name} id '{id}' is bound twice: {existing.GetType().FullName} and {handler.GetType().FullName}.");
                registry.Replace(id, handler);
            }
            if (ledger != null)
                for (var i = 0; i < registry.Count; i++)
                    if (registry[i] is IStub) ledger.Record($"{what}:{registry.Ids[i]}");
            return registry;
        }
    }
}
