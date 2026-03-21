using System;
using System.Collections.Generic;
using Reflex.Core;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;

namespace Unidad.Core.Rendering
{
    public class RenderingSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(typeof(IRenderInstanceService), typeof(RenderInstanceService));
        }

        public ISystemTestFactory CreateTestFactory() => new RenderingTestFactory();
    }

    internal class RenderingTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[] { typeof(IRenderInstanceService) };

        public object CreateForTesting(TestDependencies deps)
        {
            return new RenderInstanceService(deps.EventBus);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            // No interactive scenarios for the rendering service at this time.
            // The service is primarily tested via the InstanceGatherSystem DOTS tests.
            return Array.Empty<ITestScenario>();
        }
    }
}
