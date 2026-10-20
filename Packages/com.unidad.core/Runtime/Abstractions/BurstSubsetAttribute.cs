using System;

namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Opts an assembly into analyzer rule UD015, the Burst-compatible subset: <c>[assembly: BurstSubset]</c>.
    /// The analyzer matches the attribute by simple name, so an assembly that references nothing may declare its own copy.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class BurstSubsetAttribute : Attribute
    {
    }
}
