using System;
using System.Text;

namespace Unidad.Core.Random
{
    /// <summary>
    /// Derives a child seed from a parent seed and a salt, murmur3-finalised (fmix32).
    /// <para>
    /// <see cref="DeterministicRandom"/> only XOR-scrambles its seed and <c>NextFloat</c> reads the
    /// high bits, so <c>new DeterministicRandom(baseSeed + i)</c> hands every <c>i</c> the same
    /// leading floats — neighbouring chunks, levels or rolls seeded that way would all open the
    /// same way. fmix32 avalanches every input bit across all 32 output bits, so neighbouring
    /// (seed, salt) pairs are as unrelated as distant ones.
    /// </para>
    /// </summary>
    public static class SeedMix
    {
        /// <summary>The child seed for <paramref name="salt"/> under <paramref name="seed"/>.</summary>
        public static int Derive(int seed, int salt)
        {
            unchecked
            {
                var h = (uint)seed ^ ((uint)salt * 0x9E3779B9u);
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (int)h;
            }
        }

        private const uint FnvOffsetBasis = 2166136261;
        private const uint FnvPrime = 16777619;

        /// <summary>A stable salt for <paramref name="text"/>: FNV-1a 32 over its UTF-8 bytes.</summary>
        public static int Salt(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            unchecked
            {
                uint hash = FnvOffsetBasis;
                foreach (byte b in Encoding.UTF8.GetBytes(text))
                {
                    hash ^= b;
                    hash *= FnvPrime;
                }
                return (int)hash;
            }
        }
    }
}
