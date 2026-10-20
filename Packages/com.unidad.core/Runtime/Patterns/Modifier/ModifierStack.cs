using System.Collections.Generic;
using System.Linq;

namespace Unidad.Core.Patterns.Modifier
{
    /// <summary>
    /// Collects and evaluates modifiers in priority order: higher priority first, equal priorities in insertion order (a
    /// stable sort). Only active modifiers are applied.
    /// </summary>
    public sealed class ModifierStack<TValue>
    {
        private readonly List<IModifier<TValue>> _modifiers = new();
        private bool _dirty = true;

        public IReadOnlyList<IModifier<TValue>> Modifiers => _modifiers;

        /// <summary>
        /// Moves on every Add, Remove and Clear that changes the stack, so a reader can cache a
        /// value derived from <see cref="Evaluate"/> and know when it is stale.
        /// </summary>
        public int Version { get; private set; }

        public void Add(IModifier<TValue> modifier)
        {
            _modifiers.Add(modifier);
            _dirty = true;
            Version++;
        }

        public bool Remove(string modifierId)
        {
            var index = _modifiers.FindIndex(m => m.Id == modifierId);
            if (index < 0) return false;
            _modifiers.RemoveAt(index);
            _dirty = true;
            Version++;
            return true;
        }

        public bool Has(string modifierId) => _modifiers.Any(m => m.Id == modifierId);

        public TValue Evaluate(TValue baseValue)
        {
            if (_dirty)
            {
                SortStable();
                _dirty = false;
            }

            var result = baseValue;
            foreach (var modifier in _modifiers)
            {
                if (modifier.IsActive)
                    result = modifier.Apply(result);
            }
            return result;
        }

        // Insertion sort: stable, so equal priorities keep insertion order (List.Sort is not stable).
        private void SortStable()
        {
            for (var i = 1; i < _modifiers.Count; i++)
            {
                var item = _modifiers[i];
                var j = i - 1;
                while (j >= 0 && _modifiers[j].Priority < item.Priority)
                {
                    _modifiers[j + 1] = _modifiers[j];
                    j--;
                }
                _modifiers[j + 1] = item;
            }
        }

        public void Clear()
        {
            if (_modifiers.Count > 0) Version++;
            _modifiers.Clear();
            _dirty = true;
        }
    }

    /// <summary>A <see cref="ModifierStack{TValue}"/> whose modifiers read a context.</summary>
    public sealed class ModifierStack<TValue, TContext>
    {
        private readonly List<IModifier<TValue, TContext>> _modifiers = new();
        private bool _dirty = true;

        public IReadOnlyList<IModifier<TValue, TContext>> Modifiers => _modifiers;

        public void Add(IModifier<TValue, TContext> modifier)
        {
            _modifiers.Add(modifier);
            _dirty = true;
        }

        public bool Remove(string modifierId)
        {
            var index = _modifiers.FindIndex(m => m.Id == modifierId);
            if (index < 0) return false;
            _modifiers.RemoveAt(index);
            _dirty = true;
            return true;
        }

        public bool Has(string modifierId) => _modifiers.Any(m => m.Id == modifierId);

        public TValue Evaluate(TValue baseValue, TContext context)
        {
            if (_dirty)
            {
                SortStable();
                _dirty = false;
            }

            var result = baseValue;
            foreach (var modifier in _modifiers)
            {
                if (modifier.IsActive)
                    result = modifier.Apply(result, context);
            }
            return result;
        }

        // Insertion sort: stable, so equal priorities keep insertion order (List.Sort is not stable).
        private void SortStable()
        {
            for (var i = 1; i < _modifiers.Count; i++)
            {
                var item = _modifiers[i];
                var j = i - 1;
                while (j >= 0 && _modifiers[j].Priority < item.Priority)
                {
                    _modifiers[j + 1] = _modifiers[j];
                    j--;
                }
                _modifiers[j + 1] = item;
            }
        }

        public void Clear()
        {
            _modifiers.Clear();
            _dirty = true;
        }
    }
}
