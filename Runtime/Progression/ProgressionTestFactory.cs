using System;
using System.Collections.Generic;
using Unidad.Core.Testing;

namespace Unidad.Core.Progression
{
    internal sealed class ProgressionTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[] { typeof(IProgressionService) };

        public object CreateForTesting(TestDependencies deps)
        {
            return new ProgressionService(deps.EventBus);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new Scenarios.ProgressionScenario();
        }
    }
}
