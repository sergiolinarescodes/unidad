using System;
using System.Collections.Generic;
using Unidad.Core.Testing;

namespace Unidad.Core.Grid
{
    internal sealed class GridTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[] { typeof(IGridFactory) };

        public object CreateForTesting(TestDependencies deps)
        {
            return new GridFactory(deps.EventBus);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new Scenarios.GridScenario();
        }
    }
}
