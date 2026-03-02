using System;
using System.Collections.Generic;
using Unidad.Core.Testing;

namespace Unidad.Core.Inventory
{
    internal sealed class InventoryTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[] { typeof(IInventoryService) };

        public object CreateForTesting(TestDependencies deps)
        {
            return new InventoryService(deps.EventBus);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new Scenarios.InventoryScenario();
        }
    }
}
