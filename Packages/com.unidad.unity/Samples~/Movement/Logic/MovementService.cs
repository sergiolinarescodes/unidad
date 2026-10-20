using System;
using Unidad.Core.Abstractions;
using Unidad.Core.Diagnostics;
using Unidad.Core.EventBus;
using Unidad.Core.Primitives;
using Unidad.Core.Systems;
using static Unidad.Samples.Movement.MovementTuning;

namespace Unidad.Samples.Movement
{
    /// <summary>
    /// The box's movement rules (10's sample, numbers unchanged): semi-implicit Euler gravity while airborne, a level of
    /// two platforms and a hole, a landing only when crossing the platform top downwards, and a fell-off latch. Ticked in
    /// the fixed group, so <see cref="Tick"/>'s delta is the fixed step.
    /// </summary>
    internal sealed class MovementService : SystemServiceBase, IMovementService, ITickable
    {
        private float _x, _y, _vx, _vy;
        private float _desiredVx;
        private bool _jumpQueued;
        private float _pendingJumpForce;
        private bool _grounded;
        private bool _fellOff;
        private bool _hasBox;

        public MovementService(IEventBus eventBus) : base(eventBus)
        {
        }

        public MovementState State => new(new Vector2(_x, _y), new Vector2(_vx, _vy), _grounded, _fellOff);

        public void SpawnLevel()
        {
            _x = SpawnX;
            _y = RestY;
            _vx = _vy = 0f;
            _desiredVx = 0f;
            _jumpQueued = false;
            _pendingJumpForce = 0f;
            _grounded = true;
            _fellOff = false;
            _hasBox = true;
            Log.Info("[MovementService] Level spawned (box + 2 platforms + hole).");
        }

        public void MoveLeft(float speed) => _desiredVx = -Math.Abs(speed);

        public void MoveRight(float speed) => _desiredVx = Math.Abs(speed);

        public void StopHorizontal() => _desiredVx = 0f;

        public void Jump(float force)
        {
            if (!_grounded) return;
            _jumpQueued = true;
            _pendingJumpForce = force;
        }

        public void Tick(float deltaTime)
        {
            if (!_hasBox) return;

            var prevY = _y;
            _vx = _desiredVx;

            if (_jumpQueued)
            {
                _vy = _pendingJumpForce;
                _jumpQueued = false;
                SetGrounded(false);
                Publish(new PlayerJumpedEvent(_pendingJumpForce, new Vector2(_vx, _vy)));
            }

            if (!_grounded) _vy += Gravity * deltaTime;

            _x += _vx * deltaTime;
            _y += _vy * deltaTime;

            ResolveGround(prevY);

            if (!_fellOff && _y < FellOffThreshold)
            {
                _fellOff = true;
                Publish(new PlayerFellOffEvent(_y));
                Log.Info($"[MovementService] FellOff y={_y:0.00}");
            }
        }

        // Grounded: held at rest height over a platform, falling past an edge. Airborne: lands only when descending through
        // the platform top while over a platform, so the box can clear the hole or fall straight through it.
        private void ResolveGround(float prevY)
        {
            if (_grounded)
            {
                if (IsOverPlatform(_x))
                {
                    _y = RestY;
                    _vy = 0f;
                }
                else
                {
                    SetGrounded(false);
                }
                return;
            }

            if (_vy <= 0f && prevY >= RestY && _y <= RestY && IsOverPlatform(_x))
            {
                _y = RestY;
                _vy = 0f;
                SetGrounded(true);
            }
        }

        private static bool IsOverPlatform(float x) =>
            (x >= LeftPlatformMinX && x <= LeftPlatformMaxX) || (x >= RightPlatformMinX && x <= RightPlatformMaxX);

        private void SetGrounded(bool grounded)
        {
            _grounded = grounded;
            Publish(new PlayerGroundedChangedEvent(grounded));
        }
    }
}
