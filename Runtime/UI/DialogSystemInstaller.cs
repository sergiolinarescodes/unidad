using Reflex.Core;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;
using Unidad.Core.UI.Dialog;
using Unidad.Core.UI.TextAnimation;
using Unidad.Core.UI.TextAnimation.ElementAnimation;

namespace Unidad.Core.UI
{
    public sealed class DialogSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(container =>
            {
                var eventBus = container.Resolve<IEventBus>();
                var textAnimation = container.Resolve<ITextAnimationService>();
                var elementAnimator = container.Resolve<IElementAnimator>();
                return (IDialogService)new DialogService(eventBus, textAnimation, elementAnimator, null);
            }, typeof(IDialogService));
        }

        public ISystemTestFactory CreateTestFactory() => new DialogSystemTestFactory();
    }
}
