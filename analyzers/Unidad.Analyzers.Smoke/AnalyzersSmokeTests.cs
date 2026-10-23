using System.Linq;
using NUnit.Framework;

namespace Unidad.Analyzers.Smoke
{
    public sealed class AnalyzersSmokeTests
    {
        private static readonly string[] None = { };

        private const string ServiceBody = """
            using UnityEngine;
            namespace Game
            {
                class FooService
                {
                    void Run()
                    {
                        var go = new GameObject();
                        var dt = Time.deltaTime;
                        var r = new System.Random();
                        var x = GameObject.Find("x");
                    }
                }
            }
            """;

        [Test]
        public void ServiceRules_FireInServiceOnly()
        {
            Assert.That(AnalyzerHarness.Run("Cds.Room", ServiceBody), Is.EqualTo(new[] { "UD001", "UD003", "UD004", "UD013" }));
            Assert.That(AnalyzerHarness.Run("Cds.Room", ServiceBody.Replace("FooService", "FooView")), Is.EqualTo(None));
        }

        [Test]
        public void ServiceRules_IgnoreDomainLookalikes()
        {
            const string source = """
                namespace Game
                {
                    static class Input { public static int mousePosition => 0; }
                    static class Time { public static float deltaTime => 0; }
                    class FooService { int Tick(float dt) => Input.mousePosition + (int)(Time.deltaTime + dt); }
                }
                """;
            Assert.That(AnalyzerHarness.Run("Cds.Room", source), Is.EqualTo(None));
        }

        private const string Spinner = "using UnityEngine; class Spinner : MonoBehaviour { void Update() { } }";

        [Test]
        public void FrameHook_RequiresProcessOverride()
        {
            const string declared = """
                using UnityEngine;
                class ProcessOverrideAttribute : System.Attribute { public ProcessOverrideAttribute(string why) { } }
                [ProcessOverride("why")] class Spinner : MonoBehaviour { void Update() { } }
                """;
            Assert.That(AnalyzerHarness.Run("Cds.Room.View", Spinner), Is.EqualTo(new[] { "UD002" }));
            Assert.That(AnalyzerHarness.Run("Cds.Room.View", declared), Is.EqualTo(None));
            Assert.That(AnalyzerHarness.Run("Cds.Room.View", "class Spinner { void Update() { } }"), Is.EqualTo(None));
        }

        [Test]
        public void DeviceRead_AllowedInBridgeOnly()
        {
            const string view = """
                using UnityEngine;
                using UnityEngine.InputSystem;
                class RoomView { void Poll() { var a = Mouse.current; var b = Input.mousePosition; } }
                """;
            Assert.That(AnalyzerHarness.Run("Cds.Room.View", view), Is.EqualTo(new[] { "UD008", "UD008" }));
            Assert.That(AnalyzerHarness.Run("Cds.Room.View", view.Replace("RoomView", "RoomInputBridge")), Is.EqualTo(None));
        }

        [Test]
        public void Factory_And_AssetLibrary_Boundaries()
        {
            const string spawner = "using Object = UnityEngine.Object; class Spawner { Object Make(Object x) => Object.Instantiate(x); }";
            const string loader = "using UnityEngine; class Loader { Object Get() => Resources.Load(\"a\"); }";
            const string library = "using UnityEngine; namespace Unidad.Adapter { class AssetLibrary { Object Get() => Resources.Load(\"a\"); } }";
            const string editor = "class Refresher { void Go() => UnityEditor.AssetDatabase.Refresh(); }";

            Assert.That(AnalyzerHarness.Run("Cds.Room.View", spawner), Is.EqualTo(new[] { "UD010" }));
            Assert.That(AnalyzerHarness.Run("Cds.Room.View", spawner.Replace("Spawner", "GameObjectFactory")), Is.EqualTo(None));
            Assert.That(AnalyzerHarness.Run("Cds.Room.View", loader), Is.EqualTo(new[] { "UD011" }));
            Assert.That(AnalyzerHarness.Run("Unidad.Adapter", library), Is.EqualTo(None));
            Assert.That(AnalyzerHarness.Run("Cds.Editor", editor), Is.EqualTo(None));
            Assert.That(AnalyzerHarness.Run("Cds.Room.View", editor), Is.EqualTo(new[] { "UD011" }));
        }

        [Test]
        public void MutableStatic_Rules()
        {
            const string mutable = """
                using System;
                using System.Collections.Generic;
                static class Holder
                {
                    static int A;
                    static readonly List<int> B = new();
                    static int C { get; set; }
                    static event Action D;
                }
                """;
            const string immutable = """
                using System.Collections.Generic;
                static class Holder
                {
                    const int E = 1;
                    static readonly string F = "x";
                    static readonly IReadOnlyList<int> G = new List<int>();
                    static int H => E + 1;
                }
                """;
            Assert.That(AnalyzerHarness.Run("Unidad.Core", mutable), Is.EqualTo(new[] { "UD014", "UD014", "UD014", "UD014" }));
            Assert.That(AnalyzerHarness.Run("Unidad.Core", immutable), Is.EqualTo(None));
        }

        [Test]
        public void BurstSubset_FlagsManagedConstructs()
        {
            const string source = """
                using System;
                using System.Collections.Generic;
                [assembly: Cds.Sim.BurstSubset]
                namespace Cds.Sim
                {
                    public sealed class BurstSubsetAttribute : Attribute { }
                    public static class Kernel
                    {
                        static int Sum(params ReadOnlySpan<int> xs) => xs.Length;   // X
                        public static int Run()
                        {
                            var o = new object();                                   // X
                            float f = 1.5f;                                         // X
                            Span<int> s = [1, 2, 3];                                // X
                            Func<int> g = () => 1;                                  // X
                            foreach (var x in new List<int>()) { }                  // X
                            int total = Sum(1, 2);                                  // X
                            if (total < 0) throw new Exception();                   // X
                            return total + s.Length;
                        }
                    }
                }
                """;
            var lines = source.Split('\n');
            var expected = Enumerable.Range(0, lines.Length).Where(i => lines[i].Contains("// X")).ToArray();
            var flagged = AnalyzerHarness.Diagnostics("Cds.Sim", source).Where(d => d.Id == "UD015")
                .Select(d => d.Location.GetLineSpan().StartLinePosition.Line).Distinct().OrderBy(l => l).ToArray();

            Assert.That(flagged, Is.EqualTo(expected));
            var unmarked = source.Replace("[assembly: Cds.Sim.BurstSubset]", "");
            Assert.That(AnalyzerHarness.Run("Cds.Sim", unmarked).Where(id => id == "UD015"), Is.Empty);
        }

        [Test]
        public void BurstSubset_AllowsKernelShapes()
        {
            const string kernel = """
                using System;
                [assembly: Cds.Sim.BurstSubset]
                namespace Cds.Sim
                {
                    public sealed class BurstSubsetAttribute : Attribute { }
                    public interface ISink { void Log(int value); }
                    public unsafe struct World { public int* Cards; public fixed int Items[64]; }
                    public static unsafe class Kernel
                    {
                        public static int Run<TSink>(TSink* sink, byte op) where TSink : unmanaged, ISink
                        {
                            sink->Log(1);
                            Span<int> scratch = stackalloc int[4];
                            int total = 0;
                            foreach (var x in scratch) total += x;
                            switch (op) { case 1: total++; break; default: break; }
                            return total;
                        }
                    }
                }
                """;
            const string math = """
                [assembly: Cds.Sim.DeterministicSimulation]
                namespace Cds.Sim
                {
                    public sealed class DeterministicSimulationAttribute : System.Attribute { }
                    public static class Curve { public static double At(double t) => System.Math.Sin(t); }
                }
                """;
            Assert.That(AnalyzerHarness.Run("Cds.Sim", kernel), Is.EqualTo(None));
            Assert.That(AnalyzerHarness.Run("Cds.Sim", math), Is.EqualTo(new[] { "UD009" }));
            Assert.That(AnalyzerHarness.Run("Cds.Sim", math.Replace("[assembly: Cds.Sim.DeterministicSimulation]", "")), Is.EqualTo(None));
        }

        [Test]
        public void ScopeGate_And_Namespace()
        {
            const string framework = "namespace Unidad.Core.Foo { class X { } }";
            Assert.That(AnalyzerHarness.Run("Cds.Room", framework), Is.EqualTo(new[] { "UD016" }));
            Assert.That(AnalyzerHarness.Run("Unidad.Core", framework), Is.EqualTo(None));
            Assert.That(AnalyzerHarness.Run("Unity.RenderPipelines.Universal", Spinner), Is.EqualTo(None));
            Assert.That(AnalyzerHarness.Run("Cds.Room", "class CameraTuning { float Curve(float t) => t * 1.5f; }"),
                Is.EqualTo(new[] { "UD006" }));
        }

        [Test]
        public void GamePrefix_FromGlobalConfig()
        {
            const string framework = "namespace Unidad.Core.Foo { class X { } }";
            // Unset: Cds.* is analyzed, any other game is not.
            Assert.That(AnalyzerHarness.Run("Cds.Room.View", Spinner), Is.EqualTo(new[] { "UD002" }));
            Assert.That(AnalyzerHarness.Run("Game.Room.View", Spinner), Is.EqualTo(None));
            Assert.That(AnalyzerHarness.Run("Game.Room", framework), Is.EqualTo(None));
            // Set to Game: Game.* gets every rule, UD016 included; Cds.* no longer does; Unidad.* always does.
            Assert.That(AnalyzerHarness.RunWithPrefix("Game.Room.View", "Game", Spinner), Is.EqualTo(new[] { "UD002" }));
            Assert.That(AnalyzerHarness.RunWithPrefix("Game.Room", "Game", framework), Is.EqualTo(new[] { "UD016" }));
            Assert.That(AnalyzerHarness.RunWithPrefix("Cds.Room.View", "Game", Spinner), Is.EqualTo(None));
            Assert.That(AnalyzerHarness.RunWithPrefix("Unidad.Adapter", "Game", Spinner), Is.EqualTo(new[] { "UD002" }));
            Assert.That(AnalyzerHarness.RunWithPrefix("Gameplay.Room.View", "Game", Spinner), Is.EqualTo(None));
            Assert.That(AnalyzerHarness.DiagnosticsWithPrefix("Game.Room", "Game", framework)[0].GetMessage(),
                Does.Contain("use Game.<Module>"));
        }
    }
}
