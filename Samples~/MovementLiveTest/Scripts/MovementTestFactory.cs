using System;
using System.Collections.Generic;
using Unidad.Core.Testing;

namespace Experimental.Movement
{
    /// <summary>
    /// Test factory for the Movement system. Behavioral coverage for this feature is
    /// the Live MCP Test (see <see cref="MovementLiveTestScene"/> + its scene),
    /// which exercises real physics in a running scene — far stronger than an
    /// edit-mode DataDrivenScenario. GetScenarios() is therefore intentionally empty;
    /// the harness still satisfies ISystemInstaller.CreateTestFactory().
    /// </summary>
    [NoScenariosJustified("Behavioral coverage is the Live MCP Test ('movement' scene), which exercises real physics end-to-end — edit-mode scenarios add no signal.")]
    public sealed class MovementTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[] { typeof(IMovementService) };

        public object CreateForTesting(TestDependencies deps) => null;

        public IEnumerable<ITestScenario> GetScenarios() => Array.Empty<ITestScenario>();
    }
}
