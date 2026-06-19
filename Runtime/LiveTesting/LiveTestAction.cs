using System;

namespace Unidad.Core.LiveTesting
{
    /// <summary>
    /// A named, invokable action on a live-test scene — the "picoCAD button" concept.
    /// The SAME delegate is called by the editor button panel and by the
    /// Unidad.LiveTest.RunAction MCP tool.
    /// </summary>
    public sealed class LiveTestAction
    {
        public string Id { get; }
        public string Name { get; }
        public Action<LiveTestArgs> Invoke { get; }

        public LiveTestAction(string id, string name, Action<LiveTestArgs> invoke)
        {
            Id = id;
            Name = name;
            Invoke = invoke ?? (_ => { });
        }
    }
}
