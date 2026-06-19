using System;
using System.Collections.Generic;

namespace Unidad.Core.LiveTesting
{
    /// <summary>
    /// Base class that removes boilerplate for an <see cref="ILiveTestScene"/>:
    /// register actions/probes/steps via the protected builders, and Snapshot() is
    /// provided. Subclasses supply Id/Name/ScenePath and wire their feature service.
    /// </summary>
    public abstract class LiveTestSceneBase : ILiveTestScene
    {
        private readonly List<LiveTestAction> _actions = new();
        private readonly List<LiveTestProbe> _probes = new();
        private readonly List<LiveTestStep> _plan = new();

        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string ScenePath { get; }

        public IReadOnlyList<LiveTestAction> Actions => _actions;
        public IReadOnlyList<LiveTestProbe> Probes => _probes;
        public IReadOnlyList<LiveTestStep> Plan => _plan;

        // ---- builders -------------------------------------------------------

        protected void Action(string id, string name, Action<LiveTestArgs> invoke)
            => _actions.Add(new LiveTestAction(id, name, invoke));

        protected void Probe(string id, Func<LiveTestValue> read)
            => _probes.Add(new LiveTestProbe(id, read));

        protected void Step(string label, string actionId = null, LiveTestArgs args = null,
            int wait = 0, LiveTestUntil until = null, params LiveTestAssertion[] asserts)
            => _plan.Add(new LiveTestStep(label, actionId, args, wait, until, asserts));

        protected static LiveTestArgs Args(params (string key, double value)[] pairs)
        {
            var dict = new Dictionary<string, double>();
            if (pairs != null)
                foreach (var (key, value) in pairs)
                    dict[key] = value;
            return new LiveTestArgs(dict);
        }

        protected static LiveTestUntil Until(string probeId, LiveTestOp op, double operand, int maxSteps)
            => new(probeId, op, operand, maxSteps);

        protected static LiveTestAssertion Check(string name, string probeId, LiveTestOp op,
            double operand = 0d, double tolerance = 0.0001d)
            => new(name, probeId, op, operand, tolerance);

        // ---- ILiveTestScene -------------------------------------------------

        public IReadOnlyDictionary<string, LiveTestValue> Snapshot()
        {
            var dict = new Dictionary<string, LiveTestValue>(_probes.Count);
            foreach (var p in _probes)
                dict[p.Id] = p.Read();
            return dict;
        }
    }
}
