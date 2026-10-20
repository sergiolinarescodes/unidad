using System;

namespace Unidad.Core.Random
{
    /// <summary>
    /// A small seeded PRNG (xorshift128) with a STABLE sequence.
    /// <para>
    /// Deliberately not <see cref="System.Random"/>: its algorithm is an implementation detail and
    /// has already changed between .NET versions, so the same seed can produce different numbers
    /// after an SDK upgrade. Anything pinned against it — a balance table, a replay —
    /// then moves for a reason that has nothing to do with the game. Owning eight lines of xorshift
    /// buys a sequence that is identical on every machine and every runtime, forever.
    /// </para>
    /// <para>
    /// Not thread-safe, and not cryptographic. Inject it; never construct one from a clock (UD004
    /// exists because a time-seeded RNG is untestable).
    /// </para>
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _x, _y, _z, _w;

        /// <summary>The seed this instance was created or reset with.</summary>
        public int Seed { get; private set; }

        public DeterministicRandom(int seed) => Reset(seed);

        /// <summary>Restart the sequence from <paramref name="seed"/>.</summary>
        public void Reset(int seed)
        {
            Seed = seed;

            // Scramble the seed into four non-zero words. An all-zero state is a fixed point of
            // xorshift and would emit nothing but zeros, so the constants matter.
            unchecked
            {
                _x = (uint)seed ^ 0x9E3779B9u;
                _y = (uint)seed ^ 0x85EBCA6Bu;
                _z = (uint)seed ^ 0xC2B2AE35u;
                _w = (uint)seed ^ 0x27D4EB2Fu;
                if ((_x | _y | _z | _w) == 0) _x = 0x9E3779B9u;
            }
        }

        /// <summary>The next raw 32 bits.</summary>
        public uint NextUInt()
        {
            unchecked
            {
                var t = _x ^ (_x << 11);
                _x = _y; _y = _z; _z = _w;
                return _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
            }
        }

        /// <summary>A value in [0, exclusiveMax).</summary>
        public int Next(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax), "Must be positive.");

            // Modulo is fine here: the bias is ~2^-32 for any sane bound, and a rejection loop would
            // make the sequence depend on the bound in a way that complicates pinning.
            return (int)(NextUInt() % (uint)exclusiveMax);
        }

        /// <summary>A value in [inclusiveMin, exclusiveMax).</summary>
        public int Range(int inclusiveMin, int exclusiveMax)
        {
            if (exclusiveMax <= inclusiveMin)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax), "Max must exceed min.");
            return inclusiveMin + Next(exclusiveMax - inclusiveMin);
        }

        /// <summary>A value in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / (1 << 24));
    }
}
