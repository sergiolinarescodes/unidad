using Reflex.Core;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;

namespace Unidad.Core.Resource
{
    public sealed class ResourceSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(container =>
            {
                var eventBus = container.Resolve<IEventBus>();
                return (IResourceService)new ResourceService(eventBus);
            }, typeof(IResourceService));
        }

        public ISystemTestFactory CreateTestFactory() => new ResourceTestFactory();
    }
}
