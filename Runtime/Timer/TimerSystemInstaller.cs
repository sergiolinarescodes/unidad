using Reflex.Core;
using Unidad.Core.Abstractions;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;

namespace Unidad.Core.Timer
{
    public sealed class TimerSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(container =>
            {
                var eventBus = container.Resolve<IEventBus>();
                var service = new TimerService(eventBus);
                return service;
            }, typeof(ITimerService), typeof(ITickable));
        }

        public ISystemTestFactory CreateTestFactory() => new TimerTestFactory();
    }
}
