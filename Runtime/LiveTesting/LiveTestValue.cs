using System.Globalization;

namespace Unidad.Core.LiveTesting
{
    /// <summary>
    /// A single probe value: either a number or a boolean. Kept minimal — probes
    /// read live state (position, velocity, isGrounded, ...) and serialize cleanly
    /// for both MCP payloads and the editor panel read-out.
    /// </summary>
    public readonly struct LiveTestValue
    {
        public enum Kind { Number, Bool }

        public Kind ValueKind { get; }
        public double Number { get; }
        public bool Bool { get; }

        private LiveTestValue(Kind kind, double number, bool b)
        {
            ValueKind = kind;
            Number = number;
            Bool = b;
        }

        public static LiveTestValue Of(double n) => new(Kind.Number, n, n != 0d);
        public static LiveTestValue Of(bool b) => new(Kind.Bool, b ? 1d : 0d, b);

        /// <summary>Numeric view (bool → 1/0). Used by assertion comparisons.</summary>
        public double AsNumber() => ValueKind == Kind.Bool ? (Bool ? 1d : 0d) : Number;

        /// <summary>Boolean view (number → != 0).</summary>
        public bool AsBool() => ValueKind == Kind.Bool ? Bool : Number != 0d;

        /// <summary>Boxed object for JSON serialization (bool stays bool, number stays double).</summary>
        public object Boxed() => ValueKind == Kind.Bool ? (object)Bool : Number;

        public override string ToString() =>
            ValueKind == Kind.Bool
                ? (Bool ? "true" : "false")
                : Number.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
