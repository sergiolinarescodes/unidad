using System.Collections.Generic;

namespace Unidad.Core.Testing
{
    /// <summary>
    /// Holds parameter value overrides for scenario execution.
    /// Used by the Editor Window to pass modified values to scenarios.
    /// </summary>
    public sealed class ScenarioParameterOverrides
    {
        private readonly Dictionary<string, object> _values = new();

        public void Set(string parameterName, object value)
        {
            _values[parameterName] = value;
        }

        public T Get<T>(string parameterName, T defaultValue = default)
        {
            if (_values.TryGetValue(parameterName, out var value) && value is T typed)
                return typed;
            return defaultValue;
        }

        public bool Has(string parameterName) => _values.ContainsKey(parameterName);

        public object GetRaw(string parameterName)
        {
            _values.TryGetValue(parameterName, out var value);
            return value;
        }

        /// <summary>
        /// Resolve a parameter value: use override if present, else use definition default.
        /// </summary>
        public T Resolve<T>(ScenarioParameter param)
        {
            if (_values.TryGetValue(param.Name, out var value) && value is T typed)
                return typed;
            if (param.DefaultValue is T defaultTyped)
                return defaultTyped;
            return default;
        }
    }
}
