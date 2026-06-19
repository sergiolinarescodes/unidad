using UnityEngine;

namespace Experimental.Movement
{
    /// <summary>Immutable snapshot of the box's movement state, surfaced as probes.</summary>
    public readonly struct MovementState
    {
        public Vector2 Position { get; }
        public Vector2 Velocity { get; }
        public bool IsGrounded { get; }
        public bool FellOff { get; }

        public MovementState(Vector2 position, Vector2 velocity, bool isGrounded, bool fellOff)
        {
            Position = position;
            Velocity = velocity;
            IsGrounded = isGrounded;
            FellOff = fellOff;
        }
    }
}
