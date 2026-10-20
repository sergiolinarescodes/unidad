using System;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;
using Unidad.Core.EventBus;

namespace Unidad.Flow
{
    /// <summary>Binds <see cref="IFlowService"/>, <see cref="ISceneSwitchService"/> and <see cref="IInputFocusService"/>.</summary>
    public sealed class FlowInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.AddSingleton(c => (IFlowService)new FlowService(c.Resolve<IEventBus>()), typeof(IFlowService));
            builder.AddSingleton(c => (ISceneSwitchService)new SceneSwitchService(c.Resolve<IEventBus>(), c.Resolve<IFlowService>()),
                typeof(ISceneSwitchService));
            builder.AddSingleton(c => (IInputFocusService)new InputFocusService(c.Resolve<IEventBus>()), typeof(IInputFocusService));
        }
    }
}
