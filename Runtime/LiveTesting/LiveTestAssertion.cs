namespace Unidad.Core.LiveTesting
{
    /// <summary>Comparison operators for a live-test assertion against a probe value.</summary>
    public enum LiveTestOp
    {
        Gt,
        Gte,
        Lt,
        Lte,
        ApproxEq,
        IsTrue,
        IsFalse,
    }

    /// <summary>
    /// One named assertion: "probe {ProbeId} {Op} {Operand}" (with Tolerance for ApproxEq).
    /// Evaluated against a probe snapshot by <see cref="LiveTestAsserter"/>.
    /// </summary>
    public sealed class LiveTestAssertion
    {
        public string Name { get; }
        public string ProbeId { get; }
        public LiveTestOp Op { get; }
        public double Operand { get; }
        public double Tolerance { get; }

        public LiveTestAssertion(string name, string probeId, LiveTestOp op, double operand = 0d, double tolerance = 0.0001d)
        {
            Name = name;
            ProbeId = probeId;
            Op = op;
            Operand = operand;
            Tolerance = tolerance;
        }
    }
}
