using System;
using System.Collections.Generic;

namespace Unidad.Launch
{
    /// <summary>
    /// The process's <c>--set key=value</c> pairs. A key given twice, a malformed pair or any other argument is an error
    /// that names it; a key no module declared fails the boot (<see cref="RequireKnownSettings"/>).
    /// </summary>
    public sealed record LaunchArgs
    {
        public const string SetFlag = "--set";

        public static LaunchArgs Empty => throw new NotImplementedException();

        /// <summary>The pairs, keyed ordinal.</summary>
        public IReadOnlyDictionary<string, string> Settings => throw new NotImplementedException();

        /// <summary>The raw value, or null when the key is absent.</summary>
        public string Get(string key) => throw new NotImplementedException();

        /// <summary>False when absent; an ArgumentException naming key=value when not a whole number.</summary>
        public bool TryGetInt(string key, out int value) => throw new NotImplementedException();

        /// <summary>true/1/on, false/0/off (case-insensitive); false when absent; an ArgumentException otherwise.</summary>
        public bool TryGetBool(string key, out bool value) => throw new NotImplementedException();

        public bool IsOn(string key) => throw new NotImplementedException();

        /// <summary>Throws when a key is not in <paramref name="known"/> (listing every known key and its help) or a key is declared twice.</summary>
        public void RequireKnownSettings(IEnumerable<LaunchSettingKey> known) => throw new NotImplementedException();

        public static bool TryParse(IReadOnlyList<string> args, out LaunchArgs parsed, out string error) => throw new NotImplementedException();

        /// <summary>As <see cref="TryParse"/>, throwing ArgumentException with the error.</summary>
        public static LaunchArgs Parse(IReadOnlyList<string> args) => throw new NotImplementedException();
    }
}
