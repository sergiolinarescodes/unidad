using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unidad.Core.Testing
{
    /// <summary>
    /// Result of verifying a scenario's expectations.
    /// Contains individual check results for detailed reporting.
    /// </summary>
    public sealed class ScenarioVerificationResult
    {
        public bool Success { get; }
        public string FailureMessage { get; }
        public IReadOnlyList<CheckResult> Checks { get; }

        /// <summary>
        /// True when the scenario decided not to run (missing fixture, unsupported
        /// platform, etc.). The runner should ignore — not fail — these results.
        /// </summary>
        public bool IsSkipped { get; }

        /// <summary>Human-readable reason when <see cref="IsSkipped"/> is true.</summary>
        public string SkipReason { get; }

        public ScenarioVerificationResult(IReadOnlyList<CheckResult> checks)
        {
            Checks = checks;
            Success = checks.All(c => c.Passed);

            if (!Success)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Scenario verification failed:");
                foreach (var check in checks.Where(c => !c.Passed))
                {
                    sb.AppendLine($"  FAIL: {check.Name} - {check.Message}");
                }
                FailureMessage = sb.ToString();
            }
        }

        private ScenarioVerificationResult(string skipReason)
        {
            Checks = Array.Empty<CheckResult>();
            Success = true; // not a failure, but the caller should branch on IsSkipped first
            IsSkipped = true;
            SkipReason = skipReason ?? "skipped";
        }

        public int PassedCount => Checks.Count(c => c.Passed);
        public int FailedCount => Checks.Count(c => !c.Passed);
        public int TotalCount => Checks.Count;

        public static ScenarioVerificationResult Pass(string checkName)
        {
            return new ScenarioVerificationResult(new[] { new CheckResult(checkName, true, null) });
        }

        public static ScenarioVerificationResult Fail(string checkName, string message)
        {
            return new ScenarioVerificationResult(new[] { new CheckResult(checkName, false, message) });
        }

        /// <summary>
        /// Build a result that signals "scenario could not run — ignore me, don't fail".
        /// Used when a fixture (authored tracks, ONNX model, prefab) is missing in a
        /// clean clone or CI environment.
        /// </summary>
        public static ScenarioVerificationResult Skip(string reason)
        {
            return new ScenarioVerificationResult(reason);
        }

        public sealed record CheckResult(string Name, bool Passed, string Message);
    }
}
