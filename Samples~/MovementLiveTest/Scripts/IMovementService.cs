namespace Experimental.Movement
{
    /// <summary>
    /// 2D side-scroller movement for the demo box. All movement RULES live here
    /// (service layer) — the scene is presentation only. Driven each fixed step via
    /// IFixedTickable; the box is a real dynamic Rigidbody2D under Unity gravity.
    /// </summary>
    public interface IMovementService
    {
        /// <summary>Build the level: dynamic box + two static platforms with a hole.</summary>
        void SpawnLevel();

        /// <summary>Queue a jump impulse (only takes effect when grounded).</summary>
        void Jump(float force);

        /// <summary>Set leftward horizontal speed.</summary>
        void MoveLeft(float speed);

        /// <summary>Set rightward horizontal speed.</summary>
        void MoveRight(float speed);

        /// <summary>Clear horizontal motion.</summary>
        void StopHorizontal();

        /// <summary>Current live state — the probe source of truth.</summary>
        MovementState State { get; }
    }
}
