using Reflex.Core;
using Unidad.Core.Abstractions;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;

namespace Unidad.Core.Physics2D
{
    public sealed class Physics2DSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(_ =>
                (IPhysics2DEntityRegistry)new Physics2DEntityRegistry(), typeof(IPhysics2DEntityRegistry));

            builder.AddSingleton(container =>
            {
                var eventBus = container.Resolve<IEventBus>();
                var registry = container.Resolve<IPhysics2DEntityRegistry>();
                return (IPhysics2DService)new Physics2DService(eventBus, registry);
            }, typeof(IPhysics2DService));
        }

        public ISystemTestFactory CreateTestFactory() => new Physics2DSystemTestFactory();
    }
}
