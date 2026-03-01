using System.Collections.Generic;

namespace Unidad.Core.Debugger
{
    /// <summary>
    /// Interface for systems that provide debug information.
    /// Each system can register a debug provider to expose its internal state.
    /// </summary>
    public interface IDebugProvider
    {
        /// <summary>Name of the system this provider represents.</summary>
        string SystemName { get; }

        /// <summary>Get current debug info as key-value pairs.</summary>
        IReadOnlyDictionary<string, string> GetDebugInfo();

        /// <summary>Whether this system's debug info should be shown by default.</summary>
        bool IsActiveByDefault { get; }
    }
}
