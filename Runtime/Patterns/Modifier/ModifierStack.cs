using System.Collections.Generic;
using System.Linq;

namespace Unidad.Core.Patterns.Modifier
{
    /// <summary>
    /// Collects and evaluates modifiers in priority order.
    /// Higher priority modifiers execute first. Only active modifiers are applied.
    /// </summary>
    public sealed class ModifierStack<TValue>
    {
        private readonly List<IModifier<TValue>> _modifiers = new();
        private bool _dirty = true;

        public IReadOnlyList<IModifier<TValue>> Modifiers => _modifiers;

        public void Add(IModifier<TValue> modifier)
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

        public TValue Evaluate(TValue baseValue)
        {
            if (_dirty)
            {
                _modifiers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
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

        public void Clear()
        {
            _modifiers.Clear();
            _dirty = true;
        }
    }

    /// <summary>
    /// Collects and evaluates context-aware modifiers in priority order.
    /// </summary>
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
                _modifiers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
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

        public void Clear()
        {
            _modifiers.Clear();
            _dirty = true;
        }
    }
}
