using Unidad.Core.Abstractions;
using UnityEngine.InputSystem;
using static Unidad.Samples.Movement.MovementTuning;

namespace Unidad.Samples.Movement.View
{
    /// <summary>
    /// The keyboard as movement verbs, ticked in Input: A/Left and D/Right move, W/Up/Space jump (one jump per press),
    /// R respawns. A test calls the same verbs on the service directly.
    /// </summary>
    internal sealed class MovementInputBridge : ITickable
    {
        private readonly IMovementService _movement;
        private bool _jumpHeld;

        public MovementInputBridge(IMovementService movement) => _movement = movement;

        public void Tick(float deltaTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
            var right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
            if (left && !right) _movement.MoveLeft(MoveSpeed);
            else if (right && !left) _movement.MoveRight(MoveSpeed);
            else _movement.StopHorizontal();

            var jump = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed || keyboard.spaceKey.isPressed;
            if (jump && !_jumpHeld) _movement.Jump(JumpForce);
            _jumpHeld = jump;

            if (keyboard.rKey.isPressed) _movement.SpawnLevel();
        }
    }
}
