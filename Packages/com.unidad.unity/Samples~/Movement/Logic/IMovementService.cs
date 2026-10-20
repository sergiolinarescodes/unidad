namespace Unidad.Samples.Movement
{
    /// <summary>
    /// 2D side-scroller movement for the demo box: the reference shape of a Unidad system (service, installer, struct
    /// events). Pure logic: position, velocity and grounding are integrated in plain C# each fixed step.
    /// </summary>
    public interface IMovementService
    {
        /// <summary>Resets the box to its start on the left platform.</summary>
        void SpawnLevel();

        /// <summary>Queues a jump impulse; ignored while airborne.</summary>
        void Jump(float force);

        void MoveLeft(float speed);

        void MoveRight(float speed);

        void StopHorizontal();

        MovementState State { get; }
    }
}
