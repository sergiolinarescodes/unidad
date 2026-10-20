using Unidad.Core.Bootstrap;
using Unidad.Core.DI;
using Unidad.Core.EventBus;
using Unidad.Core.Ticking;

namespace Unidad.Samples.Movement
{
    /// <summary>Binds the movement service and ticks it in <see cref="TickGroup.Fixed"/>.</summary>
    public sealed class MovementInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(c => new MovementService(c.Resolve<IEventBus>()), typeof(IMovementService), typeof(MovementService));
            builder.AddMany(c => new TickBinding(c.Resolve<MovementService>(), TickGroup.Fixed));
        }
    }
}
