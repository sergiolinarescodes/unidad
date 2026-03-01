using System;
using System.Collections.Generic;
using Unidad.Core.Bootstrap;
using Unidad.Core.Testing;
using Unidad.Core.Tests.Tests.TestUtilities;

namespace Unidad.Core.Tests.Tests.Scenarios
{
    /// <summary>
    /// Generic framework for running test scenarios in NUnit.
    /// Auto-discovers all ISystemInstaller implementations and their scenarios.
    /// </summary>
    public static class ScenarioTestHelper
    {
        /// <summary>
        /// Discovers all scenarios from all system installers.
        /// Returns (installerType, scenario) tuples for NUnit TestCaseSource.
        /// </summary>
        public static IEnumerable<(Type InstallerType, ITestScenario Scenario)> DiscoverAllScenarios()
        {
            foreach (var installerType in InstallerDiscovery.FindInstallerTypes())
            {
                var installer = InstallerDiscovery.CreateInstaller(installerType);
                if (installer == null) continue;

                var factory = installer.CreateTestFactory();
                if (factory == null) continue;

                foreach (var scenario in factory.GetScenarios())
                {
                    yield return (installerType, scenario);
                }
            }
        }

        /// <summary>
        /// Creates standard test dependencies with TestEventBus.
        /// </summary>
        public static TestDependencies CreateTestDependencies()
        {
            var testBus = new TestEventBus();
            return new TestDependencies(testBus, testBus.History);
        }

        /// <summary>
        /// Runs a scenario and returns verification result.
        /// </summary>
        public static ScenarioVerificationResult RunAndVerify(ITestScenario scenario)
        {
            scenario.Execute();
            return scenario.Verify();
        }

        /// <summary>
        /// Runs a scenario with parameter overrides and returns verification result.
        /// </summary>
        public static ScenarioVerificationResult RunAndVerify(
            ITestScenario scenario, ScenarioParameterOverrides overrides)
        {
            scenario.Execute(overrides);
            return scenario.Verify();
        }
    }
}
