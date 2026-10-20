using System;
using System.Collections.Generic;

namespace Unidad.Core.Patterns.Modifier
{
    /// <summary>
    /// Collects and evaluates modifiers in priority order: higher priority first, equal priorities in insertion order (a
    /// stable sort). Only active modifiers are applied.
    /// </summary>
    public sealed class ModifierStack<TValue>
    {
        public IReadOnlyList<IModifier<TValue>> Modifiers => throw new NotImplementedException();

        /// <summary>
        /// Moves on every Add, Remove and Clear that changes the stack, so a reader can cache a
        /// value derived from <see cref="Evaluate"/> and know when it is stale.
        /// </summary>
        public int Version => throw new NotImplementedException();

        public void Add(IModifier<TValue> modifier) => throw new NotImplementedException();

        public bool Remove(string modifierId) => throw new NotImplementedException();

        public bool Has(string modifierId) => throw new NotImplementedException();

        public TValue Evaluate(TValue baseValue) => throw new NotImplementedException();

        public void Clear() => throw new NotImplementedException();
    }

    /// <summary>A <see cref="ModifierStack{TValue}"/> whose modifiers read a context.</summary>
    public sealed class ModifierStack<TValue, TContext>
    {
        public IReadOnlyList<IModifier<TValue, TContext>> Modifiers => throw new NotImplementedException();

        public void Add(IModifier<TValue, TContext> modifier) => throw new NotImplementedException();

        public bool Remove(string modifierId) => throw new NotImplementedException();

        public bool Has(string modifierId) => throw new NotImplementedException();

        public TValue Evaluate(TValue baseValue, TContext context) => throw new NotImplementedException();

        public void Clear() => throw new NotImplementedException();
    }
}
