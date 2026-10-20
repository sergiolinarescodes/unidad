namespace Unidad.Core.Random
{
    /// <summary>The <see cref="ISeedSource"/> the bootstrap binds from the boot seed.</summary>
    public sealed class SeedSource : ISeedSource
    {
        public SeedSource(int seed) => Seed = seed;

        public int Seed { get; }

        public int Derive(string salt) => SeedMix.Derive(Seed, SeedMix.Salt(salt));
    }
}
