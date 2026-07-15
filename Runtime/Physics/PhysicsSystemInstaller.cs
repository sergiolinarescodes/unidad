#if UNIDAD_PHYSICS3D // optional module: define UNIDAD_PHYSICS3D in Player Settings to compile
using Reflex.Core;
using Unidad.Core.Abstractions;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;

namespace Unidad.Core.Physics
{
    public sealed class PhysicsSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(_ =>
                (IPhysicsEntityRegistry)new PhysicsEntityRegistry(), typeof(IPhysicsEntityRegistry));

            builder.AddSingleton(container =>
            {
                var eventBus = container.Resolve<IEventBus>();
                var registry = container.Resolve<IPhysicsEntityRegistry>();
                return (IPhysicsService)new PhysicsService(eventBus, registry);
            }, typeof(IPhysicsService));
        }

        public ISystemTestFactory CreateTestFactory() => new PhysicsSystemTestFactory();
    }
}
#endif // UNIDAD_PHYSICS3D
