namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Interface for systems that need per-frame updates.
    /// Replaces MonoBehaviour.Update() — systems implement this
    /// and the bootstrap registers them with a TickRunner.
    /// Tests call Tick() directly without needing Unity's frame loop.
    /// </summary>
    public interface ITickable
    {
        /// <summary>
        /// Called once per frame (or per test step).
        /// Never read Time.deltaTime directly — use the provided deltaTime.
        /// </summary>
        void Tick(float deltaTime);
    }
}
