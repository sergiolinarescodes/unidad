using Reflex.Core;
using Unidad.Core.Abstractions;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Factory;
using Unidad.Core.Testing;

namespace Experimental.Movement
{
    /// <summary>
    /// Registers <see cref="IMovementService"/>. Requires IPhysics2DService — the
    /// bootstrap must install Physics2DSystemInstaller before this one.
    /// </summary>
    public sealed class MovementInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(container => (IMovementService)new MovementService(
                container.Resolve<IEventBus>(),
                container.Resolve<IGameObjectFactory>(),
                container.Resolve<IPhysics2DService>(),
                container.Resolve<ITimeProvider>()),
                typeof(IMovementService));
        }

        public ISystemTestFactory CreateTestFactory() => new MovementTestFactory();
    }
}
