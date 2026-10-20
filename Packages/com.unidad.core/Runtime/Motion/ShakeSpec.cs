using System;

namespace Unidad.Core.Motion
{
    /// <summary>A deterministic shake along one axis: Vibrato alternating points over Duration, from <see cref="Seed"/>.</summary>
    public readonly record struct ShakeSpec(float Duration, float Strength, int Vibrato, bool FadeOut, int Seed)
    {
        /// <summary>The signed offset at <paramref name="t"/>; 0 at t &lt;= 0 and t &gt;= Duration.</summary>
        public float Sample(float t) => throw new NotImplementedException();
    }
}
