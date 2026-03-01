using System;
using Unidad.Core.Testing;

namespace Unidad.Core.Tests.Tests.Scenarios
{
    /// <summary>
    /// Base class for data-driven scenarios.
    /// Subclass this and provide setup/action/verify logic for your system.
    /// The TestScenarioDefinition provides the declarative metadata.
    /// </summary>
    public abstract class DataDrivenScenario : ITestScenario
    {
        public TestScenarioDefinition Definition { get; }

        private ScenarioParameterOverrides _currentOverrides;

        protected DataDrivenScenario(TestScenarioDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public void Execute()
        {
            _currentOverrides = new ScenarioParameterOverrides();
            ExecuteInternal(_currentOverrides);
        }

        public void Execute(ScenarioParameterOverrides overrides)
        {
            _currentOverrides = overrides ?? new ScenarioParameterOverrides();
            ExecuteInternal(_currentOverrides);
        }

        public ScenarioVerificationResult Verify()
        {
            return VerifyInternal(_currentOverrides ?? new ScenarioParameterOverrides());
        }

        /// <summary>
        /// Implement setup and actions for this scenario.
        /// Use overrides.Resolve(param) to get parameter values.
        /// </summary>
        protected abstract void ExecuteInternal(ScenarioParameterOverrides overrides);

        /// <summary>
        /// Implement verification for this scenario.
        /// Return a ScenarioVerificationResult with all checks.
        /// </summary>
        protected abstract ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides);

        /// <summary>
        /// Helper: resolve a parameter value from overrides or definition defaults.
        /// </summary>
        protected T ResolveParam<T>(ScenarioParameterOverrides overrides, string paramName)
        {
            foreach (var param in Definition.Parameters)
            {
                if (param.Name == paramName)
                    return overrides.Resolve<T>(param);
            }
            return default;
        }
    }
}
