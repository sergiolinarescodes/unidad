using Reflex.Core;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using Unidad.Core.UI.Tooltip;

namespace Unidad.Core.UI
{
    public sealed class TooltipSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(container =>
            {
                var eventBus = container.Resolve<IEventBus>();
                var elementAnimator = container.Resolve<IElementAnimator>();
                return (ITooltipService)new TooltipService(eventBus, elementAnimator);
            }, typeof(ITooltipService));
        }

        public ISystemTestFactory CreateTestFactory() => new TooltipSystemTestFactory();
    }
}
