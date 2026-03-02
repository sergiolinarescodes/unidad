using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;
using UnityEngine;

namespace Unidad.Core.Physics2D.Scenarios
{
    /// <summary>
    /// Visual scenario: spawns a 2D floor and a falling sprite.
    /// Registers both as 2D physics entities with CollisionReporter2D.
    /// Collision events are logged live to the Console. Requires Play Mode.
    /// </summary>
    internal sealed class Physics2DCollisionEventScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter DropHeightParam = new(
            "dropHeight", "Drop Height", typeof(float), 5f, 1f, 20f);

        private static readonly ScenarioParameter BallMassParam = new(
            "ballMass", "Ball Mass", typeof(float), 1f, 0.1f, 10f);

        private static readonly ScenarioParameter FloorWidthParam = new(
            "floorWidth", "Floor Width", typeof(float), 10f, 2f, 30f);

        private IEventBus _eventBus;
        private Physics2DEntityRegistry _registry;
        private readonly List<IDisposable> _subscriptions = new();
        private Physics2DEntityId _ballId;
        private Physics2DEntityId _floorId;
        private GameObject _ball;
        private GameObject _floor;
        private float _startTime;

        public Physics2DCollisionEventScenario() : base(new TestScenarioDefinition(
            "physics2d-collision-events",
            "Physics2D Collision Events (Live)",
            "Drops a red sprite onto a green floor in 2D. Watch it collide in the Scene/Game view. " +
            "Collision events are logged live to the Console. Requires Play Mode.",
            new[] { DropHeightParam, BallMassParam, FloorWidthParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var dropHeight = ResolveParam<float>(overrides, "dropHeight");
            var ballMass = ResolveParam<float>(overrides, "ballMass");
            var floorWidth = ResolveParam<float>(overrides, "floorWidth");

            _startTime = Time.time;

            _eventBus = new EventBus.EventBus();
            _registry = new Physics2DEntityRegistry();

            // --- Floor ---
            _floor = CreateColoredSprite("[Scenario] Floor", new Color(0.2f, 0.8f, 0.3f));
            _floor.transform.SetParent(SceneRoot.transform);
            _floor.transform.localPosition = Vector3.zero;
            _floor.transform.localScale = new Vector3(floorWidth, 0.5f, 1f);

            var floorCollider = _floor.AddComponent<BoxCollider2D>();
            var floorRb = _floor.AddComponent<Rigidbody2D>();
            floorRb.bodyType = RigidbodyType2D.Kinematic;

            _floorId = _registry.Register(_floor, "floor");
            var floorReporter = _floor.AddComponent<CollisionReporter2D>();
            floorReporter.Initialize(_eventBus, _registry, _floorId);

            // --- Ball (falling sprite) ---
            _ball = CreateColoredSprite("[Scenario] Ball", new Color(0.9f, 0.2f, 0.2f));
            _ball.transform.SetParent(SceneRoot.transform);
            _ball.transform.localPosition = new Vector3(0f, dropHeight, 0f);
            _ball.transform.localScale = Vector3.one;

            var ballCollider = _ball.AddComponent<BoxCollider2D>();
            var ballRb = _ball.AddComponent<Rigidbody2D>();
            ballRb.mass = ballMass;

            _ballId = _registry.Register(_ball, "ball");
            var ballReporter = _ball.AddComponent<CollisionReporter2D>();
            ballReporter.Initialize(_eventBus, _registry, _ballId);

            // --- Subscribe ---
            _subscriptions.Add(_eventBus.Subscribe<Collision2DBeginEvent>(OnCollisionBegin));
            _subscriptions.Add(_eventBus.Subscribe<Collision2DEndEvent>(OnCollisionEnd));
            _subscriptions.Add(_eventBus.Subscribe<Trigger2DEnterEvent>(OnTriggerEnter));
            _subscriptions.Add(_eventBus.Subscribe<Trigger2DExitEvent>(OnTriggerExit));

            Debug.Log($"[Physics2DScenario] Started — Ball={_ballId} Floor={_floorId} " +
                      $"dropHeight={dropHeight} mass={ballMass} floorWidth={floorWidth}");
        }

        private void OnCollisionBegin(Collision2DBeginEvent evt)
        {
            var tagA = ResolveTag(evt.EntityA);
            var tagB = ResolveTag(evt.EntityB);
            Debug.Log($"[Physics2DScenario] [{Elapsed()}] COLLISION BEGIN: {tagA} <-> {tagB} " +
                      $"| speed={evt.RelativeSpeed:F2} contact={evt.ContactPoint} normal={evt.ContactNormal}");
        }

        private void OnCollisionEnd(Collision2DEndEvent evt)
        {
            var tagA = ResolveTag(evt.EntityA);
            var tagB = ResolveTag(evt.EntityB);
            Debug.Log($"[Physics2DScenario] [{Elapsed()}] COLLISION END: {tagA} <-> {tagB}");
        }

        private void OnTriggerEnter(Trigger2DEnterEvent evt)
        {
            var tagEntity = ResolveTag(evt.EntityId);
            var tagTrigger = ResolveTag(evt.TriggerId);
            Debug.Log($"[Physics2DScenario] [{Elapsed()}] TRIGGER ENTER: {tagEntity} -> {tagTrigger}");
        }

        private void OnTriggerExit(Trigger2DExitEvent evt)
        {
            var tagEntity = ResolveTag(evt.EntityId);
            var tagTrigger = ResolveTag(evt.TriggerId);
            Debug.Log($"[Physics2DScenario] [{Elapsed()}] TRIGGER EXIT: {tagEntity} -> {tagTrigger}");
        }

        private string ResolveTag(Physics2DEntityId id)
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
                new("Ball has Rigidbody2D", _ball != null && _ball.GetComponent<Rigidbody2D>() != null,
                    _ball != null && _ball.GetComponent<Rigidbody2D>() != null ? null : "Missing Rigidbody2D"),
                new("Ball registered as 2D physics entity", _ballId.IsValid,
                    _ballId.IsValid ? null : "Ball ID is None"),
                new("Floor registered as 2D physics entity", _floorId.IsValid,
                    _floorId.IsValid ? null : "Floor ID is None"),
                new("Ball has CollisionReporter2D", _ball != null && _ball.GetComponent<CollisionReporter2D>() != null,
                    _ball != null && _ball.GetComponent<CollisionReporter2D>() != null ? null : "Missing CollisionReporter2D"),
                new("Floor has CollisionReporter2D", _floor != null && _floor.GetComponent<CollisionReporter2D>() != null,
                    _floor != null && _floor.GetComponent<CollisionReporter2D>() != null ? null : "Missing CollisionReporter2D")
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

        private static GameObject CreateColoredSprite(string name, Color color)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return go;
        }
    }
}
