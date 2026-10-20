using System;

namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Opts an assembly into analyzer rule UD009: <c>[assembly: DeterministicSimulation]</c>.
    /// <para>
    /// A simulation that must compute the same bits on every machine (a server replay, lockstep
    /// multiplayer) cannot call the platform's libm transcendentals or the System.Numerics vector
    /// helpers .NET 9 rewrote; <see cref="Unidad.Core.Primitives.DetMath"/> has the replacements.
    /// An assembly without the attribute is free to use <c>MathF</c>.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class DeterministicSimulationAttribute : Attribute
    {
    }
}
