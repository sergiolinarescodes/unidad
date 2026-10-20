using System;

namespace Unidad.Core.Bootstrap
{
    /// <summary>
    /// Marks an assembly whose installers the bootstrap discovers: <c>[assembly: UnidadModule("Cds.Cards")]</c>. Assemblies
    /// without it are never scanned. Matched by simple name by the analyzers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class UnidadModuleAttribute : Attribute
    {
        public string Name { get; }

        public UnidadModuleAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A [UnidadModule] needs a name.", nameof(name));
            Name = name;
        }
    }
}
