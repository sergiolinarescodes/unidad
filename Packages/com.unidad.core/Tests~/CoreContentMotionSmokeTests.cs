using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Unidad.Core.Content;
using Unidad.Core.Motion;

namespace Unidad.Core.Smoke
{
    public sealed class CoreContentMotionSmokeTests
    {
        private enum Rarity { Common, Rare }

        private sealed class Card
        {
            public required string Id { get; init; }
            public int Cost { get; init; }
            public Rarity Rarity { get; init; }
        }

        private sealed class ThrowingCheck : IContentCheck
        {
            public string Name => "broken";
            public void Check(ContentReport report) => throw new InvalidOperationException("boom");
        }

        private sealed class ReportingCheck : IContentCheck
        {
            public string Name => "refs";
            public void Check(ContentReport report) => report.Error("Cards/cards.json", "unknown skill 'x'.");
        }

        private static MemoryContentSource Memory(params (string Path, string Json)[] files)
        {
            var map = new Dictionary<string, string>();
            foreach (var (path, json) in files) map.Add(path, json);
            return new MemoryContentSource(map);
        }

        [Test]
        public void ContentJson_UnknownMember_RefusedNamingPath()
        {
            var error = Assert.Throws<ContentException>(() => ContentJson.Parse<Card>("{ \"id\": \"a\", \"colour\": 2 }", "Cards/cards.json"));
            Assert.That(error.Path, Is.EqualTo("Cards/cards.json"));
            Assert.That(error.Message, Does.StartWith("Cards/cards.json: ").And.Contain("colour"));
        }

        [Test]
        public void ContentJson_EnumOrdinalRefused_NameAccepted()
        {
            Assert.Throws<ContentException>(() => ContentJson.Parse<Card>("{ \"id\": \"a\", \"rarity\": 1 }", "Cards/cards.json"));
            Assert.That(ContentJson.Parse<Card>("{ \"id\": \"a\", \"rarity\": \"Rare\" }", "Cards/cards.json").Rarity, Is.EqualTo(Rarity.Rare));
        }

        [Test]
        public void ContentJson_MissingRequiredMember_Refused()
        {
            var error = Assert.Throws<ContentException>(() => ContentJson.Parse<Card>("{ \"cost\": 3 }", "Cards/cards.json"));
            Assert.That(error.Message, Does.Contain("id"));
        }

        [Test]
        public void ContentJson_CommentsAndTrailingCommasAllowed()
        {
            var cards = ContentJson.Parse<List<Card>>("// header\n[ { \"id\": \"a\", /* inline */ \"cost\": 2, }, ]", "Cards/cards.json");
            Assert.That(cards, Has.Count.EqualTo(1));
            Assert.That(cards[0].Cost, Is.EqualTo(2));
        }

        [Test]
        public void Catalogue_MergesSuffixedFilesAcrossFolders_DuplicateIdNamesBothFiles()
        {
            var source = Memory(("Cards/cards.json", "[{ \"id\": \"a\" }]"), ("Mods/cards.extra.json", "[{ \"id\": \"b\" }]"), ("Mods/other.json", "[]"));
            Assert.That(ContentJson.Catalogue<Card>(source, "cards", c => c.Id).Ids, Is.EqualTo(new[] { "a", "b" }));

            var clash = Memory(("Cards/cards.json", "[{ \"id\": \"a\" }]"), ("Mods/cards.extra.json", "[{ \"id\": \"a\" }]"));
            var error = Assert.Throws<ContentException>(() => ContentJson.Catalogue<Card>(clash, "cards", c => c.Id));
            Assert.That(error.Message, Does.StartWith("Mods/cards.extra.json: ").And.Contain("Cards/cards.json"));
        }

        [Test]
        public void Layered_TopLayerWins_ListIsUnion_LayerOfNamesLabel_ReportCollectsEveryCheck()
        {
            var layered = new LayeredContentSource(new (string, IContentSource)[]
            {
                ("base", Memory(("Cards/cards.json", "base"), ("Cards/a.json", "a"))),
                ("mod:x", Memory(("Cards/cards.json", "mod"), ("Cards/b.json", "b"))),
            });
            Assert.That(layered.Read("Cards/cards.json"), Is.EqualTo("mod"));
            Assert.That(layered.LayerOf("Cards/a.json"), Is.EqualTo("base"));
            Assert.That(layered.LayerOf("Cards/cards.json"), Is.EqualTo("mod:x"));
            Assert.That(layered.LayerOf("Cards/none.json"), Is.Null);
            Assert.That(layered.List("Cards", "*.json"), Is.EqualTo(new[] { "Cards/a.json", "Cards/b.json", "Cards/cards.json" }));
            Assert.That(Assert.Throws<ContentException>(() => layered.Read("Cards/none.json")).Message, Is.EqualTo("Cards/none.json: does not exist."));
            Assert.That(ContentPath.LayerOf("mymod:Cards/x.json"), Is.EqualTo("mymod"));
            Assert.That(ContentPath.LayerOf("Cards/x.json"), Is.Null);

            var report = ContentReport.Run(new IContentCheck[] { new ThrowingCheck(), new ReportingCheck() });
            Assert.That(report.Errors, Is.EqualTo(new[] { "broken: boom", "Cards/cards.json: unknown skill 'x'." }));
            var all = Assert.Throws<ContentException>(report.ThrowIfAny);
            Assert.That(all.Path, Is.EqualTo("broken"));
            Assert.That(all.Message, Does.Contain("2 content error(s):"));
            Assert.DoesNotThrow(new ContentReport().ThrowIfAny);
        }

        [Test]
        public void Directory_RefusesDotDotPath()
        {
            var source = new DirectoryContentSource("content-root-that-does-not-exist");
            var error = Assert.Throws<ContentException>(() => source.Read("Cards/../../secret.json"));
            Assert.That(error.Message, Does.Contain("outside the content root"));
        }

        [Test]
        public void Easing_AllCurvesHitEndpoints_OutBackOvershoots()
        {
            foreach (Ease ease in Enum.GetValues(typeof(Ease)))
            {
                Assert.That(Easing.Evaluate(ease, 0f), Is.EqualTo(0f), ease.ToString());
                Assert.That(Easing.Evaluate(ease, 1f), Is.EqualTo(1f), ease.ToString());
                Assert.That(Easing.Evaluate(ease, 2f), Is.EqualTo(1f), ease.ToString());
            }
            Assert.That(Easing.Evaluate(Ease.OutBack, 0.7f), Is.GreaterThan(1f));
            Assert.That(Easing.Evaluate(Ease.InOutSine, 0.5f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(Easing.Evaluate(Ease.OutCubic, 0.5f), Is.EqualTo(0.875f).Within(1e-6f));
            Assert.That(new MotionSpec(1f, Ease.Linear, Delay: 0.5f).Progress(0.25f), Is.EqualTo(0f));
            Assert.That(new MotionSpec(1f, Ease.Linear, Delay: 0.5f).Progress(1f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(new MotionSpec(0f, Ease.Linear, Delay: 0.5f).Progress(0.5f), Is.EqualTo(1f));
        }

        [Test]
        public void Arc_HalfwayIsHeightAboveEasedChord_Punch_PeakAndHold()
        {
            var mid = Arc.Sample(Vector3.Zero, new Vector3(10f, 0f, 0f), 0.3f, 0.5f);
            Assert.That(mid.X, Is.EqualTo(8.75f).Within(1e-5f));
            Assert.That(mid.Y, Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(Arc.Sample(Vector3.Zero, Vector3.One, 0.3f, 1f), Is.EqualTo(Vector3.One));

            Span<Vector3> trail = stackalloc Vector3[11];
            Bezier2.Fill(Vector3.Zero, new Vector3(2f, 0f, 0f), 1f, trail);
            Assert.That(trail[0], Is.EqualTo(Vector3.Zero));
            Assert.That(trail[10], Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.That(trail[5].Y, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(Bezier2.RevealCount(0f, 0.3f, 11), Is.EqualTo(1));
            Assert.That(Bezier2.RevealCount(0.15f, 0.3f, 11), Is.EqualTo(6));
            Assert.That(Bezier2.RevealCount(1f, 0.3f, 11), Is.EqualTo(11));

            var punch = new PunchSpec(1.6f, 0.12f, 0.10f, 0.15f);
            Assert.That(punch.Sample(-1f), Is.EqualTo(1f));
            Assert.That(punch.Sample(punch.PeakTime), Is.EqualTo(1.6f));
            Assert.That(punch.Sample(0.2f), Is.EqualTo(1.6f));
            Assert.That(punch.Sample(punch.Duration), Is.EqualTo(1f));
            Assert.That(new PunchSpec(1.3f, 0.12f, 0f, 0.20f).Sample(0.06f), Is.InRange(1f, 1.3f));

            var shake = new ShakeSpec(0.12f, 2f, 2, false, 7);
            Assert.That(shake.Sample(0f), Is.EqualTo(0f));
            Assert.That(shake.Sample(0.12f), Is.EqualTo(0f));
            Assert.That(shake.Sample(0.04f), Is.InRange(1f, 2f));
            Assert.That(shake.Sample(0.08f), Is.InRange(-2f, -1f));
            Assert.That(shake.Sample(0.05f), Is.EqualTo(new ShakeSpec(0.12f, 2f, 2, false, 7).Sample(0.05f)));
        }

        [Test]
        public void Typewriter_SentencePauseDelaysNextChar_MarkAtZeroFiresOnFirstAdvance_MarkupFree()
        {
            var writer = new Typewriter("Hi. <b>Yo</b>", new TypewriterSpec(10f, 0.5f), new[] { 0, 4 });
            Assert.That(writer.Advance(0f), Is.EqualTo(new[] { 0 }));
            Assert.That(writer.VisibleChars, Is.EqualTo(0));

            writer.Advance(0.3f);
            Assert.That(writer.VisibleChars, Is.EqualTo(3));
            writer.Advance(0.1f);
            Assert.That(writer.VisibleChars, Is.EqualTo(3), "the space after '.' waits for the sentence pause");
            writer.Advance(0.5f);
            Assert.That(writer.VisibleChars, Is.EqualTo(4));
            Assert.That(writer.VisibleRawLength, Is.EqualTo(7), "the opening tag shows with the space, at no cost");

            Assert.That(writer.Advance(0.1f), Is.Empty);
            Assert.That(writer.VisibleChars, Is.EqualTo(5));
            writer.Advance(0.1f);
            Assert.That(writer.IsDone, Is.True);
            Assert.That(writer.VisibleRawLength, Is.EqualTo(writer.Text.Length));

            var skipped = new Typewriter("Hello", new TypewriterSpec(48f, 0.45f), new[] { 2, 9 });
            Assert.That(skipped.Complete(), Is.EqualTo(new[] { 2, 9 }));
            Assert.That(skipped.IsDone, Is.True);
        }

        [Test]
        public void Timeline_FiresByTimeThenInsertion_CompleteFiresRest_CueLifetime_HoldStopsAging()
        {
            var timeline = new Timeline<string>();
            timeline.Add(0.5f, "after");
            timeline.Add(0.2f, "contact");
            timeline.Add(0f, "before");
            timeline.Add(0.2f, "contact-2");
            Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Add(-1f, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Add(float.NaN, "x"));

            Assert.That(timeline.Advance(0f), Is.EqualTo(new[] { "before" }));
            Assert.That(timeline.Advance(0.2f), Is.EqualTo(new[] { "contact", "contact-2" }));
            timeline.Add(0.1f, "late");
            Assert.That(timeline.Advance(0f), Is.EqualTo(new[] { "late" }));
            Assert.That(timeline.Complete(), Is.EqualTo(new[] { "after" }));
            Assert.That(timeline.Time, Is.EqualTo(0.5f));
            Assert.That(timeline.IsDone, Is.True);

            var cues = new CueLifetime<string>();
            var held = cues.Add("held", 1f);
            cues.Add("short", 0.5f);
            cues.Add("sticky", float.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => cues.Add("bad", 0f));
            int version = cues.Version;
            Assert.That(cues.Hold(held, true), Is.True);
            Assert.That(cues.Version, Is.GreaterThan(version));

            Assert.That(cues.Advance(2f), Is.EqualTo(new[] { "short" }));
            Assert.That(cues.Current, Is.EqualTo(new[] { "held", "sticky" }));
            Assert.That(cues.Remaining(held), Is.EqualTo(1f));
            cues.Hold(held, false);
            Assert.That(cues.Advance(1f), Is.EqualTo(new[] { "held" }));
            Assert.That(cues.Remaining(held), Is.EqualTo(0f));
            Assert.That(cues.Count, Is.EqualTo(1));
        }
    }
}
