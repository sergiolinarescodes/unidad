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

        public sealed record CheckResult(string Name, bool Passed, string Message);
    }
}
