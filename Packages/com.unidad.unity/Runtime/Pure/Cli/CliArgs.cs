using System;
using System.Collections.Generic;

namespace Unidad.Adapter
{
    /// <summary>Reads <c>-name value</c> pairs and bare <c>-flag</c>s from an editor batch command line (names are exact).</summary>
    internal sealed class CliArgs
    {
        private readonly IReadOnlyList<string> _args;

        public CliArgs(IReadOnlyList<string> args) => _args = args ?? Array.Empty<string>();

        public static CliArgs FromProcess() => new(Environment.GetCommandLineArgs());

        public bool Flag(string name)
        {
            foreach (var arg in _args)
                if (string.Equals(arg, name, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Every value of a repeatable argument, in order. A name with no value after it throws.</summary>
        public IReadOnlyList<string> Values(string name)
        {
            var values = new List<string>();
            for (var i = 0; i < _args.Count; i++)
            {
                if (!string.Equals(_args[i], name, StringComparison.Ordinal)) continue;
                if (i + 1 >= _args.Count || _args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException($"{name} needs a value.");
                values.Add(_args[++i]);
            }

            return values;
        }

        /// <summary>The last value, or null when the argument is absent.</summary>
        public string Value(string name)
        {
            var values = Values(name);
            return values.Count == 0 ? null : values[values.Count - 1];
        }

        public string Required(string name) =>
            Value(name) ?? throw new ArgumentException($"{name} <value> is required.");
    }
}
