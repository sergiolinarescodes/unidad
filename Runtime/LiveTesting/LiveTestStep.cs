using System;
using System.Collections.Generic;

namespace Unidad.Core.LiveTesting
{
    /// <summary>
    /// A "step until a probe satisfies a condition" directive (max N fixed steps).
    /// Used instead of hardcoded wait counts so plans are deterministic and
    /// robust to force/timing changes.
    /// </summary>
    public sealed class LiveTestUntil
    {
        public string ProbeId { get; }
        public LiveTestOp Op { get; }
        public double Operand { get; }
        public int MaxSteps { get; }

        public LiveTestUntil(string probeId, LiveTestOp op, double operand, int maxSteps)
        {
            ProbeId = probeId;
            Op = op;
            Operand = operand;
            MaxSteps = maxSteps;
        }
    }

    /// <summary>
    /// One scripted beat of a live-test Plan: optionally invoke an action, then
    /// advance physics (either a fixed number of steps OR until a condition),
    /// then evaluate assertions on the resulting probe snapshot. Pure data, so the
    /// SAME plan is replayable by the editor "Run Plan" button and the MCP RunPlan tool.
    /// </summary>
    public sealed class LiveTestStep
    {
        public string Label { get; }
        public string ActionId { get; }
        public LiveTestArgs Args { get; }
        public int WaitFixedSteps { get; }
        public LiveTestUntil Until { get; }
        public IReadOnlyList<LiveTestAssertion> Assertions { get; }

        public LiveTestStep(string label, string actionId, LiveTestArgs args, int waitFixedSteps,
            LiveTestUntil until, IReadOnlyList<LiveTestAssertion> assertions)
        {
            Label = label;
            ActionId = actionId;
            Args = args ?? LiveTestArgs.Empty;
            WaitFixedSteps = waitFixedSteps;
            Until = until;
            Assertions = assertions ?? Array.Empty<LiveTestAssertion>();
        }
    }
}
