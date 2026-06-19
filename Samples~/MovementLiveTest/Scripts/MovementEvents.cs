using UnityEngine;

namespace Experimental.Movement
{
    /// <summary>Published when a jump impulse is applied.</summary>
    public readonly record struct PlayerJumpedEvent(float Force, Vector2 Velocity);

    /// <summary>Published when grounded state flips.</summary>
    public readonly record struct PlayerGroundedChangedEvent(bool IsGrounded);

    /// <summary>Published once when the box falls below the fell-off threshold.</summary>
    public readonly record struct PlayerFellOffEvent(float Y);
}
