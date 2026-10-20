using System;

namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Marks a type whose static state is reset between play sessions, which exempts it from UD014. The analyzer matches the
    /// attribute by simple name, so Unity's own <c>AutoStaticsCleanup</c> in View assemblies counts the same.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class AutoStaticsCleanupAttribute : Attribute
    {
    }
}
