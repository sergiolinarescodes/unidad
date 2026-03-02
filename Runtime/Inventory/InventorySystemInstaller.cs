using Reflex.Core;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;

namespace Unidad.Core.Inventory
{
    public sealed class InventorySystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(container =>
            {
                var eventBus = container.Resolve<IEventBus>();
                return (IInventoryService)new InventoryService(eventBus);
            }, typeof(IInventoryService));
        }

        public ISystemTestFactory CreateTestFactory() => new InventoryTestFactory();
    }
}
