using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.Testing;

namespace Unidad.Core.Physics2D
{
    internal sealed class Physics2DSystemTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[]
        {
            typeof(IPhysics2DService),
            typeof(IPhysics2DEntityRegistry)
        };

        public object CreateForTesting(TestDependencies deps)
        {
            var registry = new Physics2DEntityRegistry();
            return new Physics2DService(deps.EventBus, registry);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new Scenarios.Physics2DEntityRegistrationScenario();
            yield return new Scenarios.Physics2DCollisionEventScenario();
        }
    }
}
