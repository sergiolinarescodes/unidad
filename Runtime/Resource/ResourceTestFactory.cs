using System;
using System.Collections.Generic;
using Unidad.Core.Testing;

namespace Unidad.Core.Resource
{
    internal sealed class ResourceTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[] { typeof(IResourceService) };

        public object CreateForTesting(TestDependencies deps)
        {
            return new ResourceService(deps.EventBus);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new Scenarios.ResourceScenario();
        }
    }
}
