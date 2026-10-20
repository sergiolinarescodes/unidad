using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Unidad.Launch
{
    /// <summary>
    /// The process's <c>--set key=value</c> pairs. A key given twice, a malformed pair or any other argument is an error
    /// that names it; a key no module declared fails the boot (<see cref="RequireKnownSettings"/>).
    /// </summary>
    public sealed record LaunchArgs
    {
        public const string SetFlag = "--set";

        private LaunchArgs(IReadOnlyDictionary<string, string> settings) => Settings = settings;

        public static LaunchArgs Empty => new(new Dictionary<string, string>(StringComparer.Ordinal));

        /// <summary>The pairs, keyed ordinal.</summary>
        public IReadOnlyDictionary<string, string> Settings { get; }

        /// <summary>The raw value, or null when the key is absent.</summary>
        public string Get(string key) => key != null && Settings.TryGetValue(key, out var value) ? value : null;

        /// <summary>False when absent; an ArgumentException naming key=value when not a whole number.</summary>
        public bool TryGetInt(string key, out int value)
        {
            value = 0;
            var text = Get(key);
            if (text == null) return false;
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                throw new ArgumentException($"{SetFlag} {key}={text}: the value must be a whole number such as 7.");
            return true;
        }

        /// <summary>true/1/on, false/0/off (case-insensitive); false when absent; an ArgumentException otherwise.</summary>
        public bool TryGetBool(string key, out bool value)
        {
            value = false;
            var text = Get(key);
            if (text == null) return false;
            switch (text.ToLowerInvariant())
            {
                case "true" or "1" or "on": value = true; return true;
                case "false" or "0" or "off": value = false; return true;
                default: throw new ArgumentException($"{SetFlag} {key}={text}: the value must be true or false.");
            }
        }

        public bool IsOn(string key) => TryGetBool(key, out bool on) && on;

        /// <summary>Throws when a key is not in <paramref name="known"/> (listing every known key and its help) or a key is declared twice.</summary>
        public void RequireKnownSettings(IEnumerable<LaunchSettingKey> known)
        {
            if (known == null) throw new ArgumentNullException(nameof(known));
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in known)
                if (!keys.TryAdd(key.Key, key.Help)) throw new InvalidOperationException($"Two modules declare the launch setting '{key.Key}'.");
            var unknown = new List<string>();
            foreach (var key in Settings.Keys)
                if (!keys.ContainsKey(key)) unknown.Add(key);
            if (unknown.Count == 0) return;
            unknown.Sort(StringComparer.Ordinal);
            var list = new List<string>();
            foreach (var pair in keys) list.Add($"  {pair.Key}: {pair.Value}");
            list.Sort(StringComparer.Ordinal);
            throw new ArgumentException($"Unknown launch setting {string.Join(", ", unknown)} ({SetFlag} key=value). The known settings are:"
                + Environment.NewLine + string.Join(Environment.NewLine, list));
        }

        public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out LaunchArgs parsed, [NotNullWhen(false)] out string error)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            parsed = null;
            var settings = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Count; i++)
            {
                string flag = args[i];
                if (flag != SetFlag)
                {
                    error = $"Unknown launch argument '{flag}': the game reads {SetFlag} <key=value>.";
                    return false;
                }
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"{SetFlag} needs a value.";
                    return false;
                }
                string pair = args[++i];
                int equals = pair.IndexOf('=');
                string key = equals > 0 ? pair.Substring(0, equals).Trim() : "";
                string value = equals > 0 ? pair.Substring(equals + 1).Trim() : "";
                if (key.Length == 0 || value.Length == 0)
                {
                    error = $"{SetFlag} '{pair}' must be key=value, such as seed=7.";
                    return false;
                }
                if (!settings.TryAdd(key, value))
                {
                    error = $"{SetFlag} {key} is given twice.";
                    return false;
                }
            }
            parsed = new LaunchArgs(settings);
            error = null;
            return true;
        }

        /// <summary>As <see cref="TryParse"/>, throwing ArgumentException with the error.</summary>
        public static LaunchArgs Parse(IReadOnlyList<string> args) =>
            TryParse(args, out var parsed, out var error) ? parsed : throw new ArgumentException(error);
    }
}
