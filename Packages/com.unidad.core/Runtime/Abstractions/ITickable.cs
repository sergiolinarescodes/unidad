namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// A system that needs per-frame updates. The bootstrap registers it with the TickRunner in a
    /// <see cref="Unidad.Core.Ticking.TickGroup"/>; tests call <see cref="Tick"/> directly.
    /// </summary>
    public interface ITickable
    {
        /// <summary>
        /// Called once per frame (or per test step, or once per interval for a fixed-rate
        /// registration). Never read the engine clock — use the provided deltaTime.
        /// </summary>
        void Tick(float deltaTime);
    }
}
