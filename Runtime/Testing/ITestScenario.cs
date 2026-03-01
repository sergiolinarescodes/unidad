namespace Unidad.Core.Testing
{
    /// <summary>
    /// Interface for an executable test scenario.
    /// Can run both in NUnit and in-game via Editor Windows.
    /// </summary>
    public interface ITestScenario
    {
        /// <summary>The scenario definition with metadata and parameters.</summary>
        TestScenarioDefinition Definition { get; }

        /// <summary>Execute the scenario setup and actions.</summary>
        void Execute();

        /// <summary>Execute with custom parameter overrides (from Editor Window).</summary>
        void Execute(ScenarioParameterOverrides overrides);

        /// <summary>Verify all expectations and return result.</summary>
        ScenarioVerificationResult Verify();

        /// <summary>Clean up all resources created by this scenario (GameObjects, services, etc.).</summary>
        void Reset();
    }
}
