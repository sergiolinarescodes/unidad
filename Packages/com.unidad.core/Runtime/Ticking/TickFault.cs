using System;

namespace Unidad.Core.Ticking
{
    /// <summary>A tickable that threw, and was therefore disabled, with the exception it threw.</summary>
    public readonly record struct TickFault(string Name, TickGroup Group, Exception Error);
}
