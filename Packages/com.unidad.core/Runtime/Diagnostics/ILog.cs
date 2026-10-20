using System;

namespace Unidad.Core.Diagnostics
{
    /// <summary>
    /// Logging seam for the pure-C# core. The core NEVER references an engine logger directly —
    /// it routes through here. The default implementation writes to <see cref="System.Console"/>;
    /// the Unity adapter installs one that forwards to Debug.Log / Debug.LogError.
    /// </summary>
    public interface ILog
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
        void Exception(Exception exception);
    }
}
