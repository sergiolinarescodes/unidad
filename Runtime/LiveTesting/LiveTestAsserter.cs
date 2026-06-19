using System;
using System.Collections.Generic;
using System.Linq;
using Unidad.Core.Testing;

namespace Unidad.Core.LiveTesting
{
    /// <summary>
    /// Evaluates <see cref="LiveTestAssertion"/>s against a probe snapshot, reusing
    /// the framework's <see cref="ScenarioVerificationResult"/> for uniform reporting
    /// with the existing scenario system.
    /// </summary>
    public static class LiveTestAsserter
    {
        public static bool Compare(double actual, LiveTestOp op, double operand, double tolerance)
        {
            switch (op)
            {
                case LiveTestOp.Gt: return actual > operand;
                case LiveTestOp.Gte: return actual >= operand;
                case LiveTestOp.Lt: return actual < operand;
                case LiveTestOp.Lte: return actual <= operand;
                case LiveTestOp.ApproxEq: return Math.Abs(actual - operand) <= tolerance;
                case LiveTestOp.IsTrue: return actual != 0d;
                case LiveTestOp.IsFalse: return actual == 0d;
                default: return false;
            }
        }

        public static ScenarioVerificationResult.CheckResult Evaluate(
            LiveTestAssertion a, IReadOnlyDictionary<string, LiveTestValue> snapshot)
        {
            if (a == null)
                return new ScenarioVerificationResult.CheckResult("(null)", false, "null assertion");
            if (snapshot == null || !snapshot.TryGetValue(a.ProbeId, out var val))
                return new ScenarioVerificationResult.CheckResult(
                    a.Name, false, $"probe '{a.ProbeId}' not found");

            var actual = val.AsNumber();
            var passed = Compare(actual, a.Op, a.Operand, a.Tolerance);
            var message = passed
                ? null
                : $"{a.ProbeId}={val} failed: expected {a.Op} {a.Operand.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            return new ScenarioVerificationResult.CheckResult(a.Name, passed, message);
        }

        public static ScenarioVerificationResult Evaluate(
            IEnumerable<LiveTestAssertion> asserts, IReadOnlyDictionary<string, LiveTestValue> snapshot)
        {
            var checks = (asserts ?? Enumerable.Empty<LiveTestAssertion>())
                .Select(a => Evaluate(a, snapshot))
                .ToList();
            if (checks.Count == 0)
                checks.Add(new ScenarioVerificationResult.CheckResult("no-assertions", true, null));
            return new ScenarioVerificationResult(checks);
        }

        /// <summary>Parse an operator from MCP string form ("&gt;", "gte", "true", ...).</summary>
        public static bool TryParseOp(string s, out LiveTestOp op)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case ">": case "gt": op = LiveTestOp.Gt; return true;
                case ">=": case "gte": op = LiveTestOp.Gte; return true;
                case "<": case "lt": op = LiveTestOp.Lt; return true;
                case "<=": case "lte": op = LiveTestOp.Lte; return true;
                case "==": case "=": case "eq": case "approx": case "approxeq": op = LiveTestOp.ApproxEq; return true;
                case "true": case "istrue": op = LiveTestOp.IsTrue; return true;
                case "false": case "isfalse": op = LiveTestOp.IsFalse; return true;
                default: op = LiveTestOp.Gt; return false;
            }
        }
    }
}
