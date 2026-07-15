#if UNIDAD_PHYSICS3D // optional module: define UNIDAD_PHYSICS3D in Player Settings to compile
using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;
using UnityEngine;

namespace Unidad.Core.Physics.Scenarios
{
    /// <summary>
    /// Visual scenario: spawns a floor and a falling cube in the scene.
    /// Registers both as physics entities with CollisionReporters.
    /// Collision events are logged live to the Unity Console (Debug.Log).
    /// Requires Play Mode for physics simulation.
    /// </summary>
    internal sealed class PhysicsCollisionEventScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter DropHeightParam = new(
            "dropHeight", "Drop Height", typeof(float), 5f, 1f, 20f);

        private static readonly ScenarioParameter BallMassParam = new(
            "ballMass", "Ball Mass", typeof(float), 1f, 0.1f, 10f);

        private static readonly ScenarioParameter FloorSizeParam = new(
            "floorSize", "Floor Size", typeof(float), 10f, 2f, 30f);

        private IEventBus _eventBus;
        private PhysicsEntityRegistry _registry;
        private readonly List<IDisposable> _subscriptions = new();
        private PhysicsEntityId _ballId;
        private PhysicsEntityId _floorId;
        private GameObject _ball;
        private GameObject _floor;
        private float _startTime;

        public PhysicsCollisionEventScenario() : base(new TestScenarioDefinition(
            "physics-collision-events",
            "Physics Collision Events (Live)",
            "Drops a red cube onto a green floor. Watch it collide in the Scene/Game view. " +
            "Collision events are logged live to the Console. Requires Play Mode.",
            new[] { DropHeightParam, BallMassParam, FloorSizeParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var dropHeight = ResolveParam<float>(overrides, "dropHeight");
            var ballMass = ResolveParam<float>(overrides, "ballMass");
            var floorSize = ResolveParam<float>(overrides, "floorSize");

            _startTime = Time.time;

            // --- Services ---
            _eventBus = new EventBus.EventBus();
            _registry = new PhysicsEntityRegistry();

            // --- 3D Scene: Floor ---
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "[Scenario] Floor";
            _floor.transform.SetParent(SceneRoot.transform);
            _floor.transform.localPosition = Vector3.zero;
            _floor.transform.localScale = new Vector3(floorSize, 0.2f, floorSize);
            SetColor(_floor, new Color(0.2f, 0.8f, 0.3f));

            var floorRb = _floor.AddComponent<Rigidbody>();
            floorRb.isKinematic = true;

            _floorId = _registry.Register(_floor, "floor");
            var floorReporter = _floor.AddComponent<CollisionReporter>();
            floorReporter.Initialize(_eventBus, _registry, _floorId);

            // --- 3D Scene: Ball (falling cube) ---
            _ball = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ball.name = "[Scenario] Ball";
            _ball.transform.SetParent(SceneRoot.transform);
            _ball.transform.localPosition = new Vector3(0f, dropHeight, 0f);
            _ball.transform.localScale = Vector3.one;
            SetColor(_ball, new Color(0.9f, 0.2f, 0.2f));

            var ballRb = _ball.AddComponent<Rigidbody>();
            ballRb.mass = ballMass;

            _ballId = _registry.Register(_ball, "ball");
            var ballReporter = _ball.AddComponent<CollisionReporter>();
            ballReporter.Initialize(_eventBus, _registry, _ballId);

            // --- Subscribe and log to Console ---
            _subscriptions.Add(_eventBus.Subscribe<CollisionBeginEvent>(OnCollisionBegin));
            _subscriptions.Add(_eventBus.Subscribe<CollisionEndEvent>(OnCollisionEnd));
            _subscriptions.Add(_eventBus.Subscribe<TriggerEnterEvent>(OnTriggerEnter));
            _subscriptions.Add(_eventBus.Subscribe<TriggerExitEvent>(OnTriggerExit));

            Debug.Log($"[PhysicsScenario] Started — Ball={_ballId} Floor={_floorId} " +
                      $"dropHeight={dropHeight} mass={ballMass} floorSize={floorSize}");
        }

        private void OnCollisionBegin(CollisionBeginEvent evt)
        {
            var tagA = ResolveTag(evt.EntityA);
            var tagB = ResolveTag(evt.EntityB);
            Debug.Log($"[PhysicsScenario] [{Elapsed()}] COLLISION BEGIN: {tagA} <-> {tagB} " +
                      $"| speed={evt.RelativeSpeed:F2} contact={evt.ContactPoint} normal={evt.ContactNormal}");
        }

        private void OnCollisionEnd(CollisionEndEvent evt)
        {
            var tagA = ResolveTag(evt.EntityA);
            var tagB = ResolveTag(evt.EntityB);
            Debug.Log($"[PhysicsScenario] [{Elapsed()}] COLLISION END: {tagA} <-> {tagB}");
        }

        private void OnTriggerEnter(TriggerEnterEvent evt)
        {
            var tagEntity = ResolveTag(evt.EntityId);
            var tagTrigger = ResolveTag(evt.TriggerId);
            Debug.Log($"[PhysicsScenario] [{Elapsed()}] TRIGGER ENTER: {tagEntity} -> {tagTrigger}");
        }

        private void OnTriggerExit(TriggerExitEvent evt)
        {
            var tagEntity = ResolveTag(evt.EntityId);
            var tagTrigger = ResolveTag(evt.TriggerId);
            Debug.Log($"[PhysicsScenario] [{Elapsed()}] TRIGGER EXIT: {tagEntity} -> {tagTrigger}");
        }

        private string ResolveTag(PhysicsEntityId id)
        {
            return _registry != null && _registry.TryGetTag(id, out var tag)
                ? $"{tag}({id.Value})"
                : $"?({id.Value})";
        }

        private string Elapsed()
        {
            return $"{Time.time - _startTime:F2}s";
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root created", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Ball spawned in scene", _ball != null,
                    _ball != null ? null : "Ball GameObject is null"),
                new("Floor spawned in scene", _floor != null,
                    _floor != null ? null : "Floor GameObject is null"),
                new("Ball has Rigidbody", _ball != null && _ball.GetComponent<Rigidbody>() != null,
                    _ball != null && _ball.GetComponent<Rigidbody>() != null ? null : "Missing Rigidbody"),
                new("Ball registered as physics entity", _ballId.IsValid,
                    _ballId.IsValid ? null : "Ball ID is None"),
                new("Floor registered as physics entity", _floorId.IsValid,
                    _floorId.IsValid ? null : "Floor ID is None"),
                new("Ball has CollisionReporter", _ball != null && _ball.GetComponent<CollisionReporter>() != null,
                    _ball != null && _ball.GetComponent<CollisionReporter>() != null ? null : "Missing CollisionReporter"),
                new("Floor has CollisionReporter", _floor != null && _floor.GetComponent<CollisionReporter>() != null,
                    _floor != null && _floor.GetComponent<CollisionReporter>() != null ? null : "Missing CollisionReporter")
            };
            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup()
        {
            foreach (var sub in _subscriptions)
                sub.Dispose();
            _subscriptions.Clear();

            _eventBus?.ClearAllSubscriptions();
            _eventBus = null;
            _registry = null;
            _ball = null;
            _floor = null;
        }

        private static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            renderer.sharedMaterial = mat;
        }
    }
}
#endif // UNIDAD_PHYSICS3D
