using System.Collections.Generic;
using System.Linq;
using Unidad.Core.LiveTesting;
using Unidad.Core.Testing;
using UnityEngine;

namespace Unidad.Core.Editor.LiveTesting
{
    /// <summary>
    /// Executes a live-test Plan (and ad-hoc Step / StepUntil) by driving
    /// <see cref="LiveTestRegistry.FixedStep"/> deterministically. Shared by the
    /// editor "Run Plan" button and the Unidad.LiveTest.RunPlan MCP tool.
    ///
    /// Works synchronously because the live-test scene runs Physics2D in
    /// simulationMode = Script: each FixedStep does tickRunner.FixedTickAll(dt) then
    /// Physics2D.Simulate(dt), so a whole plan steps inside one main-thread call with
    /// no frame-timing races.
    /// </summary>
    public static class LiveTestPlanDriver
    {
        public static float FixedDt =>
            UnityEngine.Time.fixedDeltaTime > 0f ? UnityEngine.Time.fixedDeltaTime : 0.02f;

        /// <summary>Advance N fixed steps. Returns the number actually stepped.</summary>
        public static int Step(int fixedSteps)
        {
            var fs = LiveTestRegistry.FixedStep;
            if (fs == null || fixedSteps <= 0) return 0;
            var dt = FixedDt;
            for (int i = 0; i < fixedSteps; i++)
                fs(dt);
            return fixedSteps;
        }

        /// <summary>Step until a probe satisfies a condition or maxSteps is reached.</summary>
        public static (int steps, bool met) StepUntil(ILiveTestScene scene, string probeId,
            LiveTestOp op, double operand, int maxSteps)
        {
            var fs = LiveTestRegistry.FixedStep;
            if (fs == null || scene == null) return (0, false);
            var probe = scene.Probes.FirstOrDefault(p => p.Id == probeId);
            if (probe == null) return (0, false);

            if (LiveTestAsserter.Compare(probe.Read().AsNumber(), op, operand, 0.0001d))
                return (0, true);

            var dt = FixedDt;
            for (int i = 0; i < maxSteps; i++)
            {
                fs(dt);
                if (LiveTestAsserter.Compare(probe.Read().AsNumber(), op, operand, 0.0001d))
                    return (i + 1, true);
            }
            return (maxSteps, false);
        }

        /// <summary>Run the scene's full scripted Plan and return a pass/fail report.</summary>
        public static ScenarioVerificationResult RunPlan(ILiveTestScene scene)
        {
            if (scene == null)
                return ScenarioVerificationResult.Fail("run-plan", "no active live test");
            if (LiveTestRegistry.FixedStep == null)
                return ScenarioVerificationResult.Fail("run-plan", "FixedStep unavailable — enter Play Mode");

            var checks = new List<ScenarioVerificationResult.CheckResult>();
            foreach (var step in scene.Plan)
            {
                if (!string.IsNullOrEmpty(step.ActionId))
                {
                    var action = scene.Actions.FirstOrDefault(a => a.Id == step.ActionId);
                    if (action == null)
                    {
                        checks.Add(new ScenarioVerificationResult.CheckResult(
                            step.Label ?? step.ActionId, false, $"unknown action '{step.ActionId}'"));
                        continue;
                    }
                    action.Invoke(step.Args);
                }

                if (step.Until != null)
                    StepUntil(scene, step.Until.ProbeId, step.Until.Op, step.Until.Operand, step.Until.MaxSteps);
                else if (step.WaitFixedSteps > 0)
                    Step(step.WaitFixedSteps);

                var snapshot = scene.Snapshot();
                foreach (var assertion in step.Assertions)
                    checks.Add(LiveTestAsserter.Evaluate(assertion, snapshot));
            }

            if (checks.Count == 0)
                checks.Add(new ScenarioVerificationResult.CheckResult("plan-empty", true, null));
            return new ScenarioVerificationResult(checks);
        }
    }
}
