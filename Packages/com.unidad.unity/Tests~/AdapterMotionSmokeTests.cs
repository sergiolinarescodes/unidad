using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Unidad.Adapter.Models;
using Unidad.Adapter.Motion;
using Unidad.Core.Motion;

namespace Unidad.Adapter.Smoke
{
    public sealed class AdapterMotionSmokeTests
    {
        private sealed class LinearSampler : IMotionSampler
        {
            public Vector4 Sample(in MotionCurve curve, Vector4 from, Vector4 to, MotionSpec spec, float elapsed) =>
                Vector4.Lerp(from, to, Math.Clamp((elapsed - spec.Delay) / spec.Duration, 0f, 1f));
        }

        private sealed class Probe : IMotionBinding
        {
            public Vector4 Value;
            public int Writes;
            public int Callbacks;
            public bool IsAlive { get; set; } = true;

            public Vector4 Read() => Value;

            public void Write(Vector4 value)
            {
                Value = value;
                Writes++;
            }

            public void Completed() => Callbacks++;
        }

        private sealed class FakeSource : IModelSource
        {
            public FakeSource(string name, params ModelEntry[] models)
            {
                Source = name;
                Models = models;
            }

            public string Source { get; }
            public IReadOnlyList<ModelEntry> Models { get; }
        }

        private static readonly Vector4 One = new(1f, 0f, 0f, 0f);

        private static MotionTable Table() => new(new LinearSampler());

        [Test]
        public void MotionTable_StartOnSameChannelCancelsOnlyThatChannel()
        {
            var table = Table();
            object a = new(), b = new();
            var move = new Probe();
            var scale = new Probe();
            var otherMove = new Probe();
            var first = table.Start(a, MotionChannel.Move, move, One, new MotionSpec(1f), default, false);
            table.Start(a, MotionChannel.Scale, scale, One, new MotionSpec(1f), default, false);
            table.Start(b, MotionChannel.Move, otherMove, One, new MotionSpec(1f), default, false);

            table.Start(a, MotionChannel.Move, new Probe(), One, new MotionSpec(1f), default, false);

            Assert.That(table.IsPlaying(first), Is.False);
            Assert.That(move.Callbacks, Is.Zero);
            Assert.That(table.IsPlaying(a, MotionChannel.Scale), Is.True);
            Assert.That(table.IsPlaying(b, MotionChannel.Move), Is.True);
            Assert.That(table.Active, Is.EqualTo(3));
        }

        [Test]
        public void MotionTable_DelayHoldsThenEndValueExactAndCallbackOnce()
        {
            var table = Table();
            var probe = new Probe();
            var handle = table.Start(probe, MotionChannel.Move, probe, One, new MotionSpec(1f, Delay: 0.2f), default, false);

            table.Advance(0.1f, 0.1f, false);
            Assert.That(probe.Writes, Is.Zero);

            for (var i = 0; i < 12; i++) table.Advance(0.1f, 0.1f, false);
            Assert.That(probe.Value, Is.EqualTo(One));
            Assert.That(probe.Callbacks, Is.EqualTo(1));
            Assert.That(table.IsPlaying(handle), Is.False);
            Assert.That(table.Active, Is.Zero);
        }

        [Test]
        public void MotionTable_ExactEndOnOvershootingStep_AndCompleteWritesEnd_CancelAllKeepsValue()
        {
            var table = Table();
            var probe = new Probe();
            table.Start(probe, MotionChannel.Move, probe, new Vector4(3f, 0f, 0f, 0f), new MotionSpec(1f), default, false);
            table.Advance(5f, 5f, false);
            Assert.That(probe.Value.X, Is.EqualTo(3f));

            var owner = new object();
            var completed = new Probe();
            var cancelled = new Probe();
            table.Start(owner, MotionChannel.Move, completed, One, new MotionSpec(1f), default, false);
            table.Start(owner, MotionChannel.Scale, cancelled, One, new MotionSpec(1f), default, false);
            table.Complete(owner, MotionChannel.Move);
            table.CancelAll(owner);

            Assert.That(completed.Value, Is.EqualTo(One));
            Assert.That(completed.Callbacks, Is.EqualTo(1));
            Assert.That(cancelled.Writes, Is.Zero);
            Assert.That(cancelled.Callbacks, Is.Zero);
            Assert.That(table.Active, Is.Zero);
        }

        [Test]
        public void MotionTable_SkipAllCompletesInsideStart()
        {
            var table = Table();
            table.SkipAll = true;
            var probe = new Probe();

            var handle = table.Start(probe, MotionChannel.Move, probe, One, new MotionSpec(5f), default, false);

            Assert.That(probe.Value, Is.EqualTo(One));
            Assert.That(probe.Callbacks, Is.EqualTo(1));
            Assert.That(table.IsPlaying(handle), Is.False);
            Assert.That(table.Active, Is.Zero);
        }

        [Test]
        public void MotionTable_PausedFreezesUnlessRunWhilePaused_UsesUnscaledDt()
        {
            var table = Table();
            var normal = new Probe();
            var menu = new Probe();
            table.Start(normal, MotionChannel.Move, normal, One, new MotionSpec(1f), default, false);
            table.Start(menu, MotionChannel.Opacity, menu, One, new MotionSpec(1f), default, true);

            table.Advance(0f, 0.5f, paused: true);

            Assert.That(normal.Writes, Is.Zero);
            Assert.That(menu.Value.X, Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void MotionTable_PunchEndsAtFrom()
        {
            var table = Table();
            var probe = new Probe { Value = new Vector4(2f, 2f, 2f, 0f) };
            table.Start(probe, MotionChannel.Scale, probe, new Vector4(9f), new MotionSpec(0.3f), MotionCurve.PunchOf(new PunchSpec(1.2f, 0.1f, 0f, 0.2f)), false);

            table.Advance(1f, 1f, false);

            Assert.That(probe.Value, Is.EqualTo(new Vector4(2f, 2f, 2f, 0f)));
            Assert.That(probe.Callbacks, Is.EqualTo(1));
        }

        [Test]
        public void MotionTable_ShakeEndsAtFrom()
        {
            var table = Table();
            var probe = new Probe { Value = new Vector4(0f, 0f, 30f, 0f) };
            table.Start(probe, MotionChannel.Rotate, probe, new Vector4(9f), new MotionSpec(0.5f), MotionCurve.ShakeOf(new ShakeSpec(0.5f, 10f, 6, true, 7), 2), false);

            table.Advance(0.25f, 0.25f, false);
            Assert.That(probe.Writes, Is.EqualTo(1));
            table.Advance(0.25f, 0.25f, false);

            Assert.That(probe.Value, Is.EqualTo(new Vector4(0f, 0f, 30f, 0f)));
        }

        [Test]
        public void ModelSpawn_DecoratorsRunInResolveOrderAfterInstantiate_UnknownIdListsKnown()
        {
            var catalog = new ModelCatalog(new IModelSource[]
            {
                new FakeSource("props", new ModelEntry("door", "Models/Door", 2f)),
                new FakeSource("figures", new ModelEntry("guest", "Models/Guest")),
            });
            var log = new List<string>();

            var spawned = ModelSpawn.Spawn(catalog, "door", e => { log.Add("make " + e.AssetKey); return e.Id; },
                new[] { "first", "second" }, (d, instance, e) => log.Add($"{d} {instance} x{e.Scale}"));

            Assert.That(spawned, Is.EqualTo("door"));
            Assert.That(log, Is.EqualTo(new[] { "make Models/Door", "first door x2", "second door x2" }));
            var unknown = Assert.Throws<KeyNotFoundException>(() => catalog.Get("lamp"));
            Assert.That(unknown.Message, Is.EqualTo("No model 'lamp'. Known: door, guest."));
            var twice = Assert.Throws<InvalidOperationException>(() => new ModelCatalog(new IModelSource[]
                { new FakeSource("a", new ModelEntry("x", "k")), new FakeSource("b", new ModelEntry("x", "k")) }));
            Assert.That(twice.Message, Is.EqualTo("Model 'x' is defined by a and b."));
            Assert.That(ColorProperty.Pick(p => p is "_BaseColor" or "baseColorFactor"), Is.EqualTo("baseColorFactor"));
            Assert.That(ColorProperty.Pick(_ => false), Is.Null);
        }

        [Test]
        public void PicoCadPaths_TexturePrefabManifestAndFolderSteps()
        {
            Assert.That(PicoCadPaths.Texture("Assets/PicoCadImports/pig/pig.gltf", null, "pig"), Is.EqualTo("Assets/PicoCadImports/pig/pig.png"));
            Assert.That(PicoCadPaths.Texture("Assets\\Imports\\pig.gltf", "skin.png", "pig"), Is.EqualTo("Assets/Imports/skin.png"));
            Assert.That(PicoCadPaths.Prefab("Assets/Resources/Models/Door/", "door"), Is.EqualTo("Assets/Resources/Models/Door/door.prefab"));
            Assert.That(PicoCadPaths.ManifestFor("Assets/I/pig.gltf"), Is.EqualTo("Assets/I/pig.manifest.json"));
            Assert.That(PicoCadPaths.MenuTarget("pig"), Is.EqualTo("Assets/Resources/Models/pig"));
            Assert.That(PicoCadPaths.FolderSteps("Assets/Resources/Models"),
                Is.EqualTo(new[] { ("Assets", "Resources"), ("Assets/Resources", "Models") }));
        }

        [Test]
        public void CliArgs_ReadsRepeatedAndFlagArguments()
        {
            var args = new CliArgs(new[] { "Unity.exe", "-unidadScene", "a", "-unidadScene", "b", "-unidadDev", "-buildOut", "C:/out" });

            Assert.That(args.Values("-unidadScene"), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(args.Flag("-unidadDev"), Is.True);
            Assert.That(args.Flag("-release"), Is.False);
            Assert.That(args.Required("-buildOut"), Is.EqualTo("C:/out"));
            Assert.That(args.Value("-scene"), Is.Null);
            Assert.Throws<ArgumentException>(() => new CliArgs(new[] { "-buildOut", "-development" }).Value("-buildOut"));
            Assert.Throws<ArgumentException>(() => args.Required("-unidadGltf"));
        }
    }
}
