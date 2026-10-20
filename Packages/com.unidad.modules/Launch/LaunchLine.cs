using System;
using System.Collections.Generic;

namespace Unidad.Launch
{
    /// <summary>The one place the raw process line becomes arguments (engine-free; the adapter passes argv and the env string).</summary>
    public static class LaunchLine
    {
        public const string Sentinel = "--";
        public const string EnvironmentVariable = "UNIDAD_LAUNCH";

        /// <summary>Everything after the FIRST "--"; empty when there is none.</summary>
        public static IReadOnlyList<string> AfterSentinel(IReadOnlyList<string> processArgs) => throw new NotImplementedException();

        /// <summary>Whitespace-separated, "double quotes" group, no escapes; null or blank gives empty.</summary>
        public static IReadOnlyList<string> Split(string line) => throw new NotImplementedException();

        /// <summary><see cref="AfterSentinel"/> when it is non-empty, else <see cref="Split"/> of the environment line; then <see cref="LaunchArgs.Parse"/>.</summary>
        public static LaunchArgs Read(IReadOnlyList<string> processArgs, string environmentLine) => throw new NotImplementedException();
    }
}
