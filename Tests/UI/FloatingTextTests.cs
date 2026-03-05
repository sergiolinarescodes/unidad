using NUnit.Framework;
using Unidad.Core.UI.Components;
using Unidad.Core.UI.TextAnimation;

namespace Unidad.Core.Tests.Tests.UI
{
    [TestFixture]
    public class FloatingTextTests
    {
        [Test]
        public void FloatingTextStyle_Info_HasNoRecipeName()
        {
            var style = FloatingTextStyle.Info;
            Assert.That(style.RecipeName, Is.EqualTo(""));
        }

        [Test]
        public void FloatingTextStyle_Damage_HasDamageRecipe()
        {
            var style = FloatingTextStyle.Damage;
            Assert.That(style.RecipeName, Is.EqualTo("damage"));
        }

        [Test]
        public void FloatingTextStyle_Heal_HasHealRecipe()
        {
            var style = FloatingTextStyle.Heal;
            Assert.That(style.RecipeName, Is.EqualTo("heal"));
        }

        [Test]
        public void FloatingTextStyle_Critical_HasCriticalRecipe()
        {
            var style = FloatingTextStyle.Critical;
            Assert.That(style.RecipeName, Is.EqualTo("critical"));
        }

        [Test]
        public void TextAnimationRecipe_Apply_WrapsText()
        {
            var recipe = new TextAnimationRecipe("<shake a=0.1 f=5>{0}</shake>", 1.5f);
            var result = recipe.Apply("-42");
            Assert.That(result, Does.Contain("<shake"));
            Assert.That(result, Does.Contain("-42"));
            Assert.That(result, Does.Contain("</shake>"));
        }

        [Test]
        public void TextAnimationRecipe_Apply_EmptyTemplate_ReturnsOriginal()
        {
            var recipe = new TextAnimationRecipe("", 1.0f);
            Assert.That(recipe.Apply("hello"), Is.EqualTo("hello"));
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
