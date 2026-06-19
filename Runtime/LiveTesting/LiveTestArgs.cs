using System.Collections.Generic;

namespace Unidad.Core.LiveTesting
{
    /// <summary>
    /// Loosely-typed numeric argument bag for an action, so MCP can pass
    /// {"force": 12} without a per-action params type.
    /// </summary>
    public sealed class LiveTestArgs
    {
        private readonly IReadOnlyDictionary<string, double> _numbers;

        public LiveTestArgs(IReadOnlyDictionary<string, double> numbers)
        {
            _numbers = numbers ?? new Dictionary<string, double>();
        }

        public IReadOnlyDictionary<string, double> Numbers => _numbers;

        public double Get(string key, double fallback = 0d) =>
            _numbers != null && _numbers.TryGetValue(key, out var v) ? v : fallback;

        public bool Has(string key) => _numbers != null && _numbers.ContainsKey(key);

        public static LiveTestArgs Empty { get; } = new(new Dictionary<string, double>());

        public static LiveTestArgs From(IReadOnlyDictionary<string, double> n) => new(n);
    }
}
