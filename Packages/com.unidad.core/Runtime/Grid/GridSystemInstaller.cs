using Unidad.Core.Bootstrap;
using Unidad.Core.DI;
using Unidad.Core.EventBus;

namespace Unidad.Core.Grid
{
    /// <summary>Binds <see cref="IGridFactory"/>.</summary>
    public sealed class GridSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
            => builder.AddSingleton(c => (IGridFactory)new GridFactory(c.Resolve<IEventBus>()), typeof(IGridFactory));
    }
}
