using Unidad.Core.Testing;

namespace Unidad.Core.Tests.Tests.Scenarios
{
    /// <summary>
    /// Test-assembly alias for DataDrivenScenario.
    /// The base class now lives in Runtime (Unidad.Core.Testing.DataDrivenScenario)
    /// so that test factories in Runtime can also use it.
    /// This class exists for backward compatibility with any test-side scenarios.
    /// </summary>
    public abstract class DataDrivenScenario : Unidad.Core.Testing.DataDrivenScenario
    {
        protected DataDrivenScenario(TestScenarioDefinition definition) : base(definition) { }
    }
}
