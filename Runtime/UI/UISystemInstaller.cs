using Reflex.Core;
using Unidad.Core.Bootstrap;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;
using Unidad.Core.UI.DesignSystem;
using Unidad.Core.UI.TextAnimation;
using Unidad.Core.UI.TextAnimation.ElementAnimation;

namespace Unidad.Core.UI
{
    public sealed class UISystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            // Theme
            builder.AddSingleton(container =>
            {
                var eventBus = container.Resolve<IEventBus>();
                return (IThemeService)new ThemeService(eventBus);
            }, typeof(IThemeService));

            // Text Animation
            builder.AddSingleton(_ =>
                (ITextAnimationService)new TextAnimationService(), typeof(ITextAnimationService));

            // Element Animator
            builder.AddSingleton(_ =>
                (IElementAnimator)new ElementAnimator(), typeof(IElementAnimator));
        }

        public ISystemTestFactory CreateTestFactory() => new UISystemTestFactory();
    }
}
