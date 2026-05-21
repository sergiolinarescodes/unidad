using Reflex.Core;
using Unidad.Core.Bootstrap;
using Unidad.Core.Testing;
using Unidad.Core.UI.TextAnimation;

namespace Unidad.Core.UI.Integration.TextAnimations
{
    public sealed class TextAnimationSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            builder.AddSingleton(_ =>
                (ITextAnimationService)new TextAnimationService(), typeof(ITextAnimationService));
        }

        public ISystemTestFactory CreateTestFactory() => new TextAnimationSystemTestFactory();
    }
}
