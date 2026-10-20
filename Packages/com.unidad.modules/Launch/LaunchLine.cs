using System;
using System.Collections.Generic;
using System.Text;

namespace Unidad.Launch
{
    /// <summary>The one place the raw process line becomes arguments (engine-free; the adapter passes argv and the env string).</summary>
    public static class LaunchLine
    {
        public const string Sentinel = "--";
        public const string EnvironmentVariable = "UNIDAD_LAUNCH";

        /// <summary>Everything after the FIRST "--"; empty when there is none.</summary>
        public static IReadOnlyList<string> AfterSentinel(IReadOnlyList<string> processArgs)
        {
            var rest = new List<string>();
            if (processArgs == null) return rest;
            int start = -1;
            for (int i = 0; i < processArgs.Count && start < 0; i++)
                if (processArgs[i] == Sentinel) start = i + 1;
            if (start < 0) return rest;
            for (int i = start; i < processArgs.Count; i++) rest.Add(processArgs[i]);
            return rest;
        }

        /// <summary>Whitespace-separated, "double quotes" group, no escapes; null or blank gives empty.</summary>
        public static IReadOnlyList<string> Split(string line)
        {
            var parts = new List<string>();
            if (string.IsNullOrWhiteSpace(line)) return parts;
            var current = new StringBuilder();
            bool quoted = false, open = false;
            foreach (char c in line)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                    open = true;
                }
                else if (!quoted && char.IsWhiteSpace(c))
                {
                    if (open) parts.Add(current.ToString());
                    current.Clear();
                    open = false;
                }
                else
                {
                    current.Append(c);
                    open = true;
                }
            }
            if (quoted) throw new ArgumentException($"{EnvironmentVariable} has an unterminated quote.");
            if (open) parts.Add(current.ToString());
            return parts;
        }

        /// <summary><see cref="AfterSentinel"/> when it is non-empty, else <see cref="Split"/> of the environment line; then <see cref="LaunchArgs.Parse"/>.</summary>
        public static LaunchArgs Read(IReadOnlyList<string> processArgs, string environmentLine)
        {
            var args = AfterSentinel(processArgs);
            return LaunchArgs.Parse(args.Count > 0 ? args : Split(environmentLine));
        }
    }
}
