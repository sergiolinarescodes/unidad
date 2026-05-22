using System;

namespace Unidad.Core.Testing
{
    /// <summary>
    /// Opt-out for the AllInstallers_HaveTestFactory convention test. Apply to an
    /// ISystemTestFactory implementation when the system is exercised by other
    /// means (DOTS tests, unit tests) and live scenarios add no signal.
    /// The reason string is surfaced in test output so the choice stays auditable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NoScenariosJustifiedAttribute : Attribute
    {
        public string Reason { get; }

        public NoScenariosJustifiedAttribute(string reason)
        {
            Reason = reason ?? string.Empty;
        }
    }
}
