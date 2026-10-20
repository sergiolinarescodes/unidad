using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unidad.Core.Content;
using Unidad.Text;

namespace Unidad.Text.Smoke
{
    public sealed class TextSmokeTests
    {
        private static MemoryContentSource Files(params (string Path, string Json)[] files)
        {
            var map = new Dictionary<string, string>();
            foreach (var (path, json) in files) map.Add(path, json);
            return new MemoryContentSource(map);
        }

        [Test]
        public void MergedFilesWithDefaultsAndKeySets()
        {
            var source = Files(
                ("Cards/text.cards.catalogue.en.json", "{ // names\n \"card.a.name\": \"Alpha\", \"skill.block\": \"Blocks {0} damage.\", \"skill.poison\": \"Applies {key.poison}.\" }"),
                ("Room/text.room.en.json", "{ \"room.door\": \"Door\", \"pause.title\": \"Hold on\" }"));
            var table = TextContent.Load(source, new[] { new TextKeySet("Cards.cards", new[] { "card.a.name" }) });
            var text = new TextService(table);
            Assert.That(text.Get("card.a.name"), Is.EqualTo("Alpha"));
            Assert.That(text.Get("pause.title"), Is.EqualTo("Hold on"));
            Assert.That(text.Get("quit.title"), Is.EqualTo("Quit the game?"));
            Assert.That(text.Format("skill.block", 3), Is.EqualTo("Blocks 3 damage."));
            Assert.That(text.Get("skill.poison"), Is.EqualTo("Applies {key.poison}."));

            var missing = Assert.Throws<FormatException>(() =>
                TextContent.Load(source, new[] { new TextKeySet("Shop.shop", new[] { "shop.title" }) }));
            Assert.That(missing.Message, Does.Contain("shop.title (Shop.shop)"));

            var twice = Files(("A/text.a.en.json", "{ \"k\": \"one\" }"), ("B/text.b.en.json", "{ \"k\": \"two\" }"));
            var duplicate = Assert.Throws<FormatException>(() => TextContent.Load(twice, Array.Empty<TextKeySet>()));
            Assert.That(duplicate.Message, Does.Contain("A/text.a.en.json").And.Contain("B/text.b.en.json"));
            Assert.That(TextContent.Load(Files(), Array.Empty<TextKeySet>()).Count, Is.EqualTo(TextKeys.Generic.Count));
        }

        [Test]
        public void LocaleFallsBackToEnglish_MissingKeyShowsBrackets()
        {
            var source = Files(
                ("Ui/text.ui.en.json", "{ \"hello\": \"Hello\", \"bye\": \"Bye\" }"),
                ("Ui/text.ui.es.json", "{ \"hello\": \"Hola\" }"));
            var text = new TextService(TextContent.Load(source, Array.Empty<TextKeySet>(), "es"));
            Assert.That(text.Get("hello"), Is.EqualTo("Hola"));
            Assert.That(text.Get("bye"), Is.EqualTo("Bye"));
            Assert.That(text.Get("nope"), Is.EqualTo("[nope]"));
            Assert.That(text.Has("nope"), Is.False);
            Assert.Throws<ContentException>(() => TextContent.Load(source, Array.Empty<TextKeySet>(), "fr"));
            Assert.That(IdNames.Format("item.health_potion"), Is.EqualTo("Item Health Potion"));
            Assert.That(IdNames.Format("fireBolt"), Is.EqualTo("Fire Bolt"));
        }
    }
}
