using System;

namespace Unidad.Core.Bootstrap
{
    /// <summary>
    /// Thrown when a discovered <see cref="ISystemInstaller"/> type cannot be instantiated.
    /// <para>
    /// This type exists so a broken installer CANNOT vanish. Swallowing the failure
    /// (<c>catch { return null; }</c> plus an <c>if (installer != null)</c> at the call site)
    /// produces a game that logs "N systems installed" with a system MISSING, and resurfaces the
    /// failure far away as a container <c>Resolve&lt;IFooService&gt;</c> throw or a screen that
    /// quietly does nothing. Letting it propagate routes it into
    /// <c>UnidadBootstrap.OnInitializationFailed</c> — a non-zero exit under a run plan.
    /// </para>
    /// </summary>
    public sealed class InstallerLoadException : Exception
    {
        /// <summary>The installer type that failed to construct.</summary>
        public Type InstallerType { get; }

        public InstallerLoadException(Type installerType, Exception innerException)
            : base(BuildMessage(installerType, innerException), innerException)
        {
            InstallerType = installerType;
        }

        private static string BuildMessage(Type installerType, Exception innerException)
        {
            var name = installerType?.FullName ?? "<unknown type>";
            var reason = innerException?.Message ?? "no inner exception";
            return $"Installer '{name}' could not be instantiated: {reason}. " +
                   "A system installer that cannot be constructed is a broken build, not a system " +
                   "to skip — fix the constructor or stop discovering the type.";
        }
    }
}
