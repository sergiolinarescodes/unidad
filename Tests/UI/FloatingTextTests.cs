using NUnit.Framework;
using Unidad.Core.Tests.Tests.TestUtilities;
using Unidad.Core.UI.Components;
using Unidad.Core.UI.Events;

namespace Unidad.Core.Tests.Tests.UI
{
    [TestFixture]
    public class FloatingTextTests
    {
        [Test]
        public void FloatingTextStyle_Info_HasNoAnimationTag()
        {
            var style = FloatingTextStyle.Info;
            Assert.That(style.FormatText("Hello"), Is.EqualTo("Hello"));
        }

        [Test]
        public void FloatingTextStyle_Damage_WrapsWithShakeTag()
        {
            var style = FloatingTextStyle.Damage;
            var formatted = style.FormatText("-42");
            Assert.That(formatted, Does.Contain("<shake"));
            Assert.That(formatted, Does.Contain("-42"));
            Assert.That(formatted, Does.Contain("</shake>"));
        }

        [Test]
        public void FloatingTextStyle_Heal_WrapsWithWaveTag()
        {
            var style = FloatingTextStyle.Heal;
            var formatted = style.FormatText("+15");
            Assert.That(formatted, Does.Contain("<wave"));
            Assert.That(formatted, Does.Contain("+15"));
        }

        [Test]
        public void FloatingTextStyle_Critical_WrapsWithBounceTag()
        {
            var style = FloatingTextStyle.Critical;
            var formatted = style.FormatText("CRIT!");
            Assert.That(formatted, Does.Contain("<bounce"));
            Assert.That(formatted, Does.Contain("CRIT!"));
        }

        [Test]
        public void MockTextAnimationService_PlayTypewriter_TracksText()
        {
            var mock = new MockTextAnimationService();
            var label = mock.CreateLabel();
            var completed = false;

            mock.PlayTypewriter(label, "Hello World", () => completed = true);

            Assert.That(mock.PlayedTexts.Count, Is.EqualTo(1));
            Assert.That(mock.PlayedTexts[0], Is.EqualTo("Hello World"));
            Assert.That(completed, Is.True); // Mock auto-completes
        }
    }
}
