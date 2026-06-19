using Unidad.Core.Abstractions;
using Unidad.Core.EventBus;
using Unidad.Core.Factory;
using Unidad.Core.Systems;
using UnityEngine;

namespace Experimental.Movement
{
    /// <summary>
    /// Movement logic for the 2D demo box. Dynamic Rigidbody2D under real gravity;
    /// horizontal velocity is set each fixed step (preserving the gravity-driven Y),
    /// jump is an impulse applied only when grounded, grounded is a short downward
    /// raycast onto the "Ground" layer, and fell-off latches below a Y threshold.
    ///
    /// No MonoBehaviour.Update, no direct UnityEngine.Time. The box + platforms are
    /// built via IGameObjectFactory (house style — see RoombaGame).
    /// </summary>
    internal sealed class MovementService : SystemServiceBase, IMovementService, IFixedTickable
    {
        private const float FellOffThreshold = -3f;
        private const float GroundRayDistance = 0.6f;   // a touch past the unit-box half-height
        private const float GroundedMaxHitDistance = 0.57f;

        private readonly IGameObjectFactory _factory;
        private readonly IPhysics2DService _physics;
        private readonly ITimeProvider _time;
        private readonly int _groundMask;

        private GameObject _boxGo;
        private Rigidbody2D _box;
        private float _desiredVx;
        private bool _jumpQueued;
        private float _pendingJumpForce;
        private bool _grounded;
        private bool _fellOff;

        public MovementService(IEventBus eventBus, IGameObjectFactory factory,
            IPhysics2DService physics, ITimeProvider time) : base(eventBus)
        {
            _factory = factory;
            _physics = physics;
            _time = time;
            _groundMask = LayerMask.GetMask("Ground");
        }

        public MovementState State => new(
            _box != null ? _box.position : Vector2.zero,
            _box != null ? _box.linearVelocity : Vector2.zero,
            _grounded,
            _fellOff);

        public void SpawnLevel()
        {
            _factory.DestroyAll();
            _boxGo = null;
            _box = null;
            _desiredVx = 0f;
            _jumpQueued = false;
            _grounded = false;
            _fellOff = false;

            // Dynamic box (real gravity).
            _boxGo = Create2DPrimitive("Box", new Vector3(-4f, 1f, 0f));
            _factory.SetColor(_boxGo, new Color(0.95f, 0.55f, 0.15f));
            _boxGo.AddComponent<BoxCollider2D>();
            _box = _boxGo.AddComponent<Rigidbody2D>();
            _box.gravityScale = 1f;
            _box.freezeRotation = true;
            _box.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _box.interpolation = RigidbodyInterpolation2D.Interpolate;
            _physics.RegisterEntity(_boxGo, "player");

            // Two static platforms with a hole between x in (-1, 2).
            MakePlatform("PlatformLeft", new Vector3(-4f, 0f, 0f), new Vector2(6f, 1f));
            MakePlatform("PlatformRight", new Vector3(5f, 0f, 0f), new Vector2(6f, 1f));

            Debug.Log("[MovementService] Level spawned (box + 2 platforms + hole).");
        }

        /// <summary>
        /// Create a factory-tracked cube ready for 2D physics. CreatePrimitive leaves a
        /// 3D Collider scheduled for deferred Object.Destroy; a Collider2D/Rigidbody2D
        /// cannot be added while it is still present, so we remove it immediately.
        /// </summary>
        private GameObject Create2DPrimitive(string name, Vector3 position)
        {
            var go = _factory.CreatePrimitive(PrimitiveType.Cube, name, position);
            var stale3d = go.GetComponent<Collider>();
            if (stale3d != null) Object.DestroyImmediate(stale3d);
            return go;
        }

        private void MakePlatform(string name, Vector3 position, Vector2 size)
        {
            var platform = Create2DPrimitive(name, position);
            platform.transform.localScale = new Vector3(size.x, size.y, 1f);
            _factory.SetColor(platform, new Color(0.3f, 0.35f, 0.42f));

            var groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) platform.layer = groundLayer;

            platform.AddComponent<BoxCollider2D>();
            var body = platform.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            _physics.RegisterEntity(platform, "platform");
        }

        public void MoveLeft(float speed) => _desiredVx = -Mathf.Abs(speed);
        public void MoveRight(float speed) => _desiredVx = Mathf.Abs(speed);
        public void StopHorizontal() => _desiredVx = 0f;

        public void Jump(float force)
        {
            if (!_grounded) return;
            _jumpQueued = true;
            _pendingJumpForce = force;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (_box == null) return;

            // Horizontal velocity is driven; vertical is left to gravity/jump.
            _box.linearVelocity = new Vector2(_desiredVx, _box.linearVelocity.y);

            if (_jumpQueued)
            {
                _box.linearVelocity = new Vector2(_box.linearVelocity.x, _pendingJumpForce);
                _jumpQueued = false;
                SetGrounded(false);
                Publish(new PlayerJumpedEvent(_pendingJumpForce, _box.linearVelocity));
                Debug.Log($"[MovementService] Jump force={_pendingJumpForce} vel={_box.linearVelocity}");
            }

            // Grounded: cast down from box center. The "Ground" mask (and
            // queriesStartInColliders=false, set by the bootstrap) exclude the box itself.
            var mask = _groundMask != 0 ? _groundMask : ~0;
            var hit = _physics.Raycast(_box.position, Vector2.down, out var rayHit, GroundRayDistance, mask);
            var groundedNow = hit && rayHit.distance <= GroundedMaxHitDistance;
            if (groundedNow != _grounded) SetGrounded(groundedNow);

            if (!_fellOff && _box.position.y < FellOffThreshold)
            {
                _fellOff = true;
                Publish(new PlayerFellOffEvent(_box.position.y));
                Debug.Log($"[MovementService] FellOff y={_box.position.y:0.00}");
            }
        }

        private void SetGrounded(bool grounded)
        {
            _grounded = grounded;
            Publish(new PlayerGroundedChangedEvent(grounded));
            Debug.Log($"[MovementService] Grounded={grounded}");
        }

        public override void Dispose()
        {
            _factory?.DestroyAll();
            base.Dispose();
        }
    }
}
