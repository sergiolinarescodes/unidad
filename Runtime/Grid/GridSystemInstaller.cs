using Reflex.Core;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;

namespace Unidad.Core.Grid
{
    public sealed class GridSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(container =>
            {
                var eventBus = container.Resolve<IEventBus>();
                return (IGridFactory)new GridFactory(eventBus);
            }, typeof(IGridFactory));
        }

        public ISystemTestFactory CreateTestFactory() => new GridTestFactory();
    }
}
