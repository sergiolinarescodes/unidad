using System.Collections.Generic;

namespace Unidad.Core.LiveTesting
{
    /// <summary>
    /// The contract for a Live MCP Test: a feature cluster's Play-Mode scene exposes
    /// named actions (MCP- and button-invokable), numeric/bool probes, and a scripted
    /// assertion-first Plan. Registered into <see cref="LiveTestRegistry"/> by the
    /// scene's bootstrap at Play start so Editor MCP tools + the panel can drive it.
    /// </summary>
    public interface ILiveTestScene
    {
        /// <summary>Stable id, e.g. "movement".</summary>
        string Id { get; }

        /// <summary>Human label, e.g. "2D Movement".</summary>
        string Name { get; }

        /// <summary>Asset path of the scene to load before driving this test.</summary>
        string ScenePath { get; }

        IReadOnlyList<LiveTestAction> Actions { get; }
        IReadOnlyList<LiveTestProbe> Probes { get; }

        /// <summary>The scripted end-to-end sequence the RunPlan runner executes.</summary>
        IReadOnlyList<LiveTestStep> Plan { get; }

        /// <summary>Read every probe at once — the assertion workhorse.</summary>
        IReadOnlyDictionary<string, LiveTestValue> Snapshot();
    }
}
