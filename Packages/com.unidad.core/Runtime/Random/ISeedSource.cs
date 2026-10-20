namespace Unidad.Core.Random
{
    /// <summary>
    /// The boot seed and per-purpose seeds. <see cref="Derive"/> mixes the seed with a stable hash of the salt, so every
    /// service owns its own <see cref="DeterministicRandom"/> and one purpose's draws never shift another's.
    /// </summary>
    public interface ISeedSource
    {
        int Seed { get; }

        int Derive(string salt);
    }
}
