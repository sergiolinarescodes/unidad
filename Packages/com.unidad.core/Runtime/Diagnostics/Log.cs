using System;

namespace Unidad.Core.Diagnostics
{
    /// <summary>
    /// Static entry point for the logging seam. Defaults to Console output so the core
    /// works in a plain `dotnet` / test process with zero engine dependencies. Swap
    /// <see cref="Current"/> once (e.g. in the Unity adapter's bootstrap) to redirect
    /// every core log line to the engine console. This is a deliberate global seam, not
    /// service state — services still inject their own collaborators.
    /// </summary>
    public static class Log
    {
#pragma warning disable UD014 // the sanctioned logging seam (D-19), set once by the adapter's bootstrap
        private static ILog _current = new ConsoleLog();
#pragma warning restore UD014

        /// <summary>The active logger. Setting null restores the Console default.</summary>
        public static ILog Current
        {
            get => _current;
            set => _current = value ?? new ConsoleLog();
        }

        public static void Info(string message) => _current.Info(message);
        public static void Warning(string message) => _current.Warning(message);
        public static void Error(string message) => _current.Error(message);
        public static void Exception(Exception exception) => _current.Exception(exception);

        private sealed class ConsoleLog : ILog
        {
            public void Info(string message) => Console.WriteLine(message);
            public void Warning(string message) => Console.WriteLine($"[WARN] {message}");
            public void Error(string message) => Console.Error.WriteLine($"[ERROR] {message}");
            public void Exception(Exception exception) => Console.Error.WriteLine(exception);
        }
    }
}
