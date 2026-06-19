using System;

namespace Unidad.Core.LiveTesting
{
    /// <summary>
    /// A named probe that reads one live value from the running scene. Probes are
    /// the assertion-first source of truth — Claude reads and asserts on these
    /// instead of relying on screenshots.
    /// </summary>
    public sealed class LiveTestProbe
    {
        public string Id { get; }
        public Func<LiveTestValue> Read { get; }

        public LiveTestProbe(string id, Func<LiveTestValue> read)
        {
            Id = id;
            Read = read;
        }
    }
}
