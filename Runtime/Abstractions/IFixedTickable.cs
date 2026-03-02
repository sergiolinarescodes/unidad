namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Interface for systems that need fixed-timestep updates (physics).
    /// Replaces MonoBehaviour.FixedUpdate() — systems implement this
    /// and the bootstrap registers them with a TickRunner.
    /// Tests call FixedTick() directly without needing Unity's physics loop.
    /// </summary>
    public interface IFixedTickable
    {
        /// <summary>
        /// Called once per fixed timestep (or per test step).
        /// Never read Time.fixedDeltaTime directly — use the provided fixedDeltaTime.
        /// </summary>
        void FixedTick(float fixedDeltaTime);
    }
}
