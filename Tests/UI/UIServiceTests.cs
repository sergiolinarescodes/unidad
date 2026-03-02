using NUnit.Framework;
using Unidad.Core.Tests.Tests.TestUtilities;
using Unidad.Core.UI;
using Unidad.Core.UI.DesignSystem;
using Unidad.Core.UI.Events;
using Unidad.Core.UI.TextAnimation;

namespace Unidad.Core.Tests.Tests.UI
{
    [TestFixture]
    public class UIServiceTests
    {
        [Test]
        public void ThemeService_SetTheme_PublishesEvent()
        {
            var eventBus = new MockEventBus();
            var themeService = new ThemeService(eventBus);

            themeService.SetTheme("dark", null);

            Assert.That(eventBus.HasEventOfType<ThemeChangedEvent>(), Is.True);
            var evt = eventBus.GetPublishedEvent<ThemeChangedEvent>();
            Assert.That(evt.ThemeName, Is.EqualTo("dark"));
        }

        [Test]
        public void ThemeService_CurrentTheme_UpdatesAfterSet()
        {
            var eventBus = new MockEventBus();
            var themeService = new ThemeService(eventBus);

            Assert.That(themeService.CurrentTheme, Is.EqualTo("default"));
            themeService.SetTheme("light", null);
            Assert.That(themeService.CurrentTheme, Is.EqualTo("light"));
        }

        [Test]
        public void TextAnimationService_HasDefaultPresets()
        {
            var service = new TextAnimationService();

            Assert.That(service.GetPreset("default"), Is.Not.Null);
            Assert.That(service.GetPreset("dialog"), Is.Not.Null);
            Assert.That(service.GetPreset("floating"), Is.Not.Null);
            Assert.That(service.GetPreset("title"), Is.Not.Null);
        }

        [Test]
        public void TextAnimationService_DialogPreset_HasTypewriterEnabled()
        {
            var service = new TextAnimationService();
            var dialog = service.GetPreset("dialog");

            Assert.That(dialog.enableTextAppearance, Is.True);
            Assert.That(dialog.baseAppearanceSpeed, Is.EqualTo(30f));
        }

        [Test]
        public void TextAnimationService_RegisterPreset_CanBeRetrieved()
        {
            var service = new TextAnimationService();
            var custom = TextAnimationPresets.CreateDefaultPreset();

            service.RegisterPreset("custom", custom);

            Assert.That(service.GetPreset("custom"), Is.SameAs(custom));
        }

        [Test]
        public void UISystemInstaller_CreateTestFactory_ReturnsNonNull()
        {
            var installer = new UISystemInstaller();
            var factory = installer.CreateTestFactory();
            Assert.That(factory, Is.Not.Null);
        }

        [Test]
        public void DialogSystemInstaller_CreateTestFactory_ReturnsNonNull()
        {
            var installer = new DialogSystemInstaller();
            var factory = installer.CreateTestFactory();
            Assert.That(factory, Is.Not.Null);
        }
    }
}
