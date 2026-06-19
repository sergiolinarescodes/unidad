using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unidad.Core.Editor.LiveTesting;
using Unidad.Core.LiveTesting;
using Unidad.Core.Testing;
using Unity.AI.MCP.Editor.Helpers;
using Unity.AI.MCP.Editor.ToolRegistry;
using UnityEditor;

namespace Unidad.Core.Editor.LiveTesting.Mcp
{
    /// <summary>
    /// First-class MCP tools for the Live MCP Test harness. Registered automatically
    /// by Unity's McpToolRegistry at editor load (this assembly only compiles when
    /// com.unity.ai.assistant is installed — see the asmdef's defineConstraints).
    /// Tool "Unidad.LiveTest.X" surfaces over MCP as mcp__unity-mcp__Unidad_LiveTest_X.
    ///
    /// All tools read <see cref="LiveTestRegistry.Active"/> — the scene's bootstrap
    /// populates it at Play start. They run on the editor main thread, so reaching the
    /// live runtime service is safe.
    /// </summary>
    public static class LiveTestMcpTools
    {
        // ------------------------------------------------------------- helpers

        public sealed class EmptyParams
        {
            [McpDescription("Reserved; omit.")]
            public bool Verbose { get; set; }
        }

        private static Dictionary<string, object> SnapshotPayload(ILiveTestScene scene)
            => scene.Snapshot().ToDictionary(kv => kv.Key, kv => kv.Value.Boxed());

        private static LiveTestArgs ParseArgs(string json)
        {
            var dict = new Dictionary<string, double>();
            if (string.IsNullOrWhiteSpace(json)) return LiveTestArgs.Empty;
            var s = json.Trim();
            if (s.StartsWith("{")) s = s.Substring(1);
            if (s.EndsWith("}")) s = s.Substring(0, s.Length - 1);
            foreach (var pair in s.Split(','))
            {
                if (string.IsNullOrWhiteSpace(pair)) continue;
                var idx = pair.IndexOf(':');
                if (idx < 0) continue;
                var key = pair.Substring(0, idx).Trim().Trim('"', '\'', ' ');
                var valStr = pair.Substring(idx + 1).Trim().Trim('"', '\'', ' ');
                if (double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    dict[key] = v;
            }
            return dict.Count > 0 ? new LiveTestArgs(dict) : LiveTestArgs.Empty;
        }

        private static object ResultPayload(ScenarioVerificationResult r) => new
        {
            success = r.Success,
            passed = r.PassedCount,
            failed = r.FailedCount,
            total = r.TotalCount,
            checks = r.Checks.Select(c => new { name = c.Name, passed = c.Passed, message = c.Message }).ToArray(),
        };

        // --------------------------------------------------------------- List

        [McpTool("Unidad.LiveTest.List",
            "List the live test active in the running scene: id/name/scene path, action ids, probe ids, plan step count. Errors if none is active (load its scene and enter Play Mode first).",
            "List the active live test",
            EnabledByDefault = true, Groups = new[] { "unidad", "livetest" })]
        public static object List(EmptyParams parameters)
        {
            try
            {
                var scene = LiveTestRegistry.Active;
                if (scene == null) return Response.Error("No active live test. Load its scene and enter Play Mode.");
                return Response.Success($"Active live test: {scene.Id}", new
                {
                    activeId = scene.Id,
                    activeName = scene.Name,
                    scenePath = scene.ScenePath,
                    actionIds = scene.Actions.Select(a => a.Id).ToArray(),
                    probeIds = scene.Probes.Select(p => p.Id).ToArray(),
                    planSteps = scene.Plan.Count,
                });
            }
            catch (Exception ex) { return Response.Error(ex.Message); }
        }

        // ----------------------------------------------------------- Describe

        [McpTool("Unidad.LiveTest.Describe",
            "Describe the active live test in full: each action (id+name), each probe (id), and the scripted Plan (per step: action, wait/until, assertions). Read this before running to understand the contract.",
            "Describe the active live test",
            EnabledByDefault = true, Groups = new[] { "unidad", "livetest" })]
        public static object Describe(EmptyParams parameters)
        {
            try
            {
                var scene = LiveTestRegistry.Active;
                if (scene == null) return Response.Error("No active live test.");
                return Response.Success($"{scene.Name} ({scene.Id})", new
                {
                    id = scene.Id,
                    name = scene.Name,
                    scenePath = scene.ScenePath,
                    actions = scene.Actions.Select(a => new { id = a.Id, name = a.Name }).ToArray(),
                    probes = scene.Probes.Select(p => p.Id).ToArray(),
                    plan = scene.Plan.Select(s => new
                    {
                        label = s.Label,
                        actionId = s.ActionId,
                        wait = s.WaitFixedSteps,
                        until = s.Until == null ? null : (object)new
                        {
                            probe = s.Until.ProbeId,
                            op = s.Until.Op.ToString(),
                            operand = s.Until.Operand,
                            maxSteps = s.Until.MaxSteps,
                        },
                        assertions = s.Assertions.Select(a => new
                        {
                            name = a.Name,
                            probe = a.ProbeId,
                            op = a.Op.ToString(),
                            operand = a.Operand,
                        }).ToArray(),
                    }).ToArray(),
                });
            }
            catch (Exception ex) { return Response.Error(ex.Message); }
        }

        // ---------------------------------------------------------- RunAction

        public sealed class RunActionParams
        {
            [McpDescription("Action id to invoke (see Unidad.LiveTest.List actionIds).", Required = true)]
            public string ActionId { get; set; }

            [McpDescription("Optional JSON object of numeric args, e.g. {\"force\":12,\"speed\":6}.")]
            public string Args { get; set; }
        }

        [McpTool("Unidad.LiveTest.RunAction",
            "Invoke a named action on the active live test (the same delegate the editor button calls). Returns the immediate probe snapshot. NOTE: does NOT advance physics — follow with Unidad.LiveTest.Step or .StepUntil, then read .Probe.",
            "Run a live test action",
            EnabledByDefault = true, Groups = new[] { "unidad", "livetest" })]
        public static object RunAction(RunActionParams parameters)
        {
            try
            {
                var scene = LiveTestRegistry.Active;
                if (scene == null) return Response.Error("No active live test.");
                if (string.IsNullOrEmpty(parameters?.ActionId)) return Response.Error("ActionId is required.");
                var action = scene.Actions.FirstOrDefault(a => a.Id == parameters.ActionId);
                if (action == null)
                    return Response.Error($"Unknown action '{parameters.ActionId}'. Available: {string.Join(", ", scene.Actions.Select(a => a.Id))}");
                action.Invoke(ParseArgs(parameters.Args));
                return Response.Success($"Invoked '{parameters.ActionId}'", new
                {
                    actionId = parameters.ActionId,
                    state = SnapshotPayload(scene),
                });
            }
            catch (Exception ex) { return Response.Error(ex.Message); }
        }

        // --------------------------------------------------------------- Step

        public sealed class StepParams
        {
            [McpDescription("Number of deterministic fixed physics steps to advance.", Required = true)]
            public int FixedSteps { get; set; }
        }

        [McpTool("Unidad.LiveTest.Step",
            "Advance the running scene by N deterministic fixed physics steps (Script-mode Physics2D.Simulate). Returns the probe snapshot after stepping.",
            "Step the live test",
            EnabledByDefault = true, Groups = new[] { "unidad", "livetest" })]
        public static object Step(StepParams parameters)
        {
            try
            {
                var scene = LiveTestRegistry.Active;
                if (scene == null) return Response.Error("No active live test.");
                if (LiveTestRegistry.FixedStep == null) return Response.Error("FixedStep unavailable — enter Play Mode in the live-test scene.");
                var n = Math.Max(0, parameters?.FixedSteps ?? 0);
                LiveTestPlanDriver.Step(n);
                return Response.Success($"Stepped {n}", new { stepped = n, state = SnapshotPayload(scene) });
            }
            catch (Exception ex) { return Response.Error(ex.Message); }
        }

        // ---------------------------------------------------------- StepUntil

        public sealed class StepUntilParams
        {
            [McpDescription("Probe id to watch.", Required = true)]
            public string ProbeId { get; set; }

            [McpDescription("Operator: > >= < <= == true false (or gt/gte/lt/lte/approx/istrue/isfalse).", Required = true)]
            public string Op { get; set; }

            [McpDescription("Operand to compare against (ignored for true/false).")]
            public double Operand { get; set; }

            [McpDescription("Max fixed steps before giving up.", Default = 300)]
            public int MaxSteps { get; set; } = 300;
        }

        [McpTool("Unidad.LiveTest.StepUntil",
            "Advance fixed steps until a probe satisfies a condition (or maxSteps). Deterministic — removes hardcoded wait counts. Returns steps taken, whether met, and the snapshot.",
            "Step until a probe condition",
            EnabledByDefault = true, Groups = new[] { "unidad", "livetest" })]
        public static object StepUntil(StepUntilParams parameters)
        {
            try
            {
                var scene = LiveTestRegistry.Active;
                if (scene == null) return Response.Error("No active live test.");
                if (string.IsNullOrEmpty(parameters?.ProbeId)) return Response.Error("ProbeId is required.");
                if (!LiveTestAsserter.TryParseOp(parameters.Op, out var op)) return Response.Error($"Bad operator '{parameters.Op}'.");
                var (steps, met) = LiveTestPlanDriver.StepUntil(scene, parameters.ProbeId, op, parameters.Operand, Math.Max(1, parameters.MaxSteps));
                return Response.Success(
                    $"StepUntil {parameters.ProbeId} {parameters.Op} {parameters.Operand}: met={met} after {steps} steps",
                    new { stepsTaken = steps, met, state = SnapshotPayload(scene) });
            }
            catch (Exception ex) { return Response.Error(ex.Message); }
        }

        // -------------------------------------------------------------- Probe

        [McpTool("Unidad.LiveTest.Probe",
            "Read a full snapshot of every probe on the active live test (no side effects). The assertion-first workhorse.",
            "Read live test probes",
            EnabledByDefault = true, Groups = new[] { "unidad", "livetest" })]
        public static object Probe(EmptyParams parameters)
        {
            try
            {
                var scene = LiveTestRegistry.Active;
                if (scene == null) return Response.Error("No active live test.");
                return Response.Success("Probe snapshot", new { state = SnapshotPayload(scene) });
            }
            catch (Exception ex) { return Response.Error(ex.Message); }
        }

        // ------------------------------------------------------------- Assert

        public sealed class AssertParams
        {
            [McpDescription("Probe id to assert on.", Required = true)]
            public string ProbeId { get; set; }

            [McpDescription("Operator: > >= < <= == true false.", Required = true)]
            public string Op { get; set; }

            [McpDescription("Operand to compare against (ignored for true/false).")]
            public double Operand { get; set; }

            [McpDescription("Tolerance for == (ApproxEq).", Default = 0.0001)]
            public double Tolerance { get; set; } = 0.0001;
        }

        [McpTool("Unidad.LiveTest.Assert",
            "Assert a single named-value condition against a live probe right now (no stepping). Returns passed + the actual value.",
            "Assert a live probe value",
            EnabledByDefault = true, Groups = new[] { "unidad", "livetest" })]
        public static object Assert(AssertParams parameters)
        {
            try
            {
                var scene = LiveTestRegistry.Active;
                if (scene == null) return Response.Error("No active live test.");
                if (string.IsNullOrEmpty(parameters?.ProbeId)) return Response.Error("ProbeId is required.");
                if (!LiveTestAsserter.TryParseOp(parameters.Op, out var op)) return Response.Error($"Bad operator '{parameters.Op}'.");
                var snap = scene.Snapshot();
                if (!snap.TryGetValue(parameters.ProbeId, out var val)) return Response.Error($"Unknown probe '{parameters.ProbeId}'.");
                var passed = LiveTestAsserter.Compare(val.AsNumber(), op, parameters.Operand, parameters.Tolerance);
                return Response.Success($"{parameters.ProbeId} {parameters.Op} {parameters.Operand} => {passed}", new
                {
                    passed,
                    actual = val.Boxed(),
                    expected = $"{parameters.Op} {parameters.Operand.ToString(CultureInfo.InvariantCulture)}",
                });
            }
            catch (Exception ex) { return Response.Error(ex.Message); }
        }

        // ------------------------------------------------------------ RunPlan

        [McpTool("Unidad.LiveTest.RunPlan",
            "Run the active live test's full scripted Plan (actions + deterministic steps + assertions) synchronously and return a pass/fail report. The one-call, in-scene end-to-end verification.",
            "Run the live test plan",
            EnabledByDefault = true, Groups = new[] { "unidad", "livetest" })]
        public static object RunPlan(EmptyParams parameters)
        {
            try
            {
                var scene = LiveTestRegistry.Active;
                if (scene == null) return Response.Error("No active live test.");
                var result = LiveTestPlanDriver.RunPlan(scene);
                return Response.Success($"Plan: {result.PassedCount}/{result.TotalCount} passed", ResultPayload(result));
            }
            catch (Exception ex) { return Response.Error(ex.Message); }
        }

        // ------------------------------------------------------------ ListAll

        [McpTool("Unidad.LiveTest.ListAll",
            "Enumerate ALL live tests in the project (types marked [LiveTestScene]) WITHOUT loading them: id, name, scene path. Drive a run-all loop: for each, load the scene, Play, RunPlan, Stop, then aggregate.",
            "List all live tests",
            EnabledByDefault = true, Groups = new[] { "unidad", "livetest" })]
        public static object ListAll(EmptyParams parameters)
        {
            try
            {
                var entries = new List<object>();
                foreach (var type in TypeCache.GetTypesWithAttribute<LiveTestSceneAttribute>())
                {
                    var attr = (LiveTestSceneAttribute)Attribute.GetCustomAttribute(type, typeof(LiveTestSceneAttribute));
                    if (attr == null) continue;
                    entries.Add(new { id = attr.Id, name = attr.Name, scenePath = attr.ScenePath, type = type.FullName });
                }
                return Response.Success($"{entries.Count} live test(s) registered", new { liveTests = entries.ToArray() });
            }
            catch (Exception ex) { return Response.Error(ex.Message); }
        }
    }
}
