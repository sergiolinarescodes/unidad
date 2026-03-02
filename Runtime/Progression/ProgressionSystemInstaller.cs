using Reflex.Core;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;

namespace Unidad.Core.Progression
{
    public sealed class ProgressionSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(container =>
            {
                var eventBus = container.Resolve<IEventBus>();
                return (IProgressionService)new ProgressionService(eventBus);
            }, typeof(IProgressionService));
        }

        public ISystemTestFactory CreateTestFactory() => new ProgressionTestFactory();
    }
}
