#if UNIDAD_PHYSICS3D // optional module: define UNIDAD_PHYSICS3D in Player Settings to compile
using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.Testing;

namespace Unidad.Core.Physics
{
    internal sealed class PhysicsSystemTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[]
        {
            typeof(IPhysicsService),
            typeof(IPhysicsEntityRegistry)
        };

        public object CreateForTesting(TestDependencies deps)
        {
            var registry = new PhysicsEntityRegistry();
            return new PhysicsService(deps.EventBus, registry);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new Scenarios.PhysicsEntityRegistrationScenario();
            yield return new Scenarios.PhysicsCollisionEventScenario();
        }
    }
}
#endif // UNIDAD_PHYSICS3D
