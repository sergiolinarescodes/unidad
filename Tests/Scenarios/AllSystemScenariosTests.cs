using System.Collections.Generic;
using NUnit.Framework;
using Unidad.Core.Testing;

namespace Unidad.Core.Tests.Tests.Scenarios
{
    /// <summary>
    /// Auto-discovers and executes ALL scenarios from ALL system installers.
    /// This is the core enforcement mechanism: if a system has scenarios, they run here.
    /// </summary>
    [TestFixture]
    public class AllSystemScenariosTests
    {
        private static IEnumerable<TestCaseData> AllScenarios()
        {
            foreach (var (installerType, scenario) in ScenarioTestHelper.DiscoverAllScenarios())
            {
                yield return new TestCaseData(scenario)
                    .SetName($"{installerType.Name} > {scenario.Definition.Name}")
                    .SetDescription(scenario.Definition.Description);
            }
        }

        [TestCaseSource(nameof(AllScenarios))]
        public void Scenario_Passes(ITestScenario scenario)
        {
            scenario.Execute();
            var result = scenario.Verify();

            Assert.That(result.Success, Is.True,
                result.FailureMessage ?? "Scenario failed with no message");
        }
    }
}
