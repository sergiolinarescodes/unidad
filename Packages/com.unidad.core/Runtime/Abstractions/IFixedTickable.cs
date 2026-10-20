namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// A system that needs fixed-timestep updates (physics). The bootstrap registers it with the
    /// TickRunner; tests call <see cref="FixedTick"/> directly without an engine physics loop.
    /// </summary>
    public interface IFixedTickable
    {
        /// <summary>
        /// Called once per fixed timestep (or per test step).
        /// Never read the engine's physics step — use the provided fixedDeltaTime.
        /// </summary>
        void FixedTick(float fixedDeltaTime);
    }
}
