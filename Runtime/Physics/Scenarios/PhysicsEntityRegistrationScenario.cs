using System.Collections.Generic;
using Unidad.Core.Testing;
using UnityEngine;

namespace Unidad.Core.Physics.Scenarios
{
    /// <summary>
    /// Visual scenario: spawns colored cubes, registers them as physics entities,
    /// then unregisters the last one. Registration state is logged to the Console.
    /// </summary>
    internal sealed class PhysicsEntityRegistrationScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter EntityCountParam = new(
            "entityCount", "Entity Count", typeof(int), 3, 1, 6);

        private static readonly ScenarioParameter SpacingParam = new(
            "spacing", "Spacing Between Cubes", typeof(float), 2.5f, 1f, 5f);

        private PhysicsEntityRegistry _registry;
        private readonly List<(GameObject Go, PhysicsEntityId Id, string Tag)> _entities = new();
        private PhysicsEntityId _unregisteredId;
        private bool _unregisterLookupIsNone;
        private bool _unregisterTryGetFails;
        private bool _idempotentReRegisterWorks;

        private static readonly Color[] EntityColors =
        {
            new(0.9f, 0.2f, 0.2f),
            new(0.2f, 0.5f, 0.9f),
            new(0.2f, 0.8f, 0.3f),
            new(0.9f, 0.8f, 0.1f),
            new(0.7f, 0.3f, 0.9f),
            new(0.9f, 0.5f, 0.1f)
        };

        private static readonly string[] EntityTags =
        {
            "player", "enemy", "item", "projectile", "obstacle", "trigger-zone"
        };

        public PhysicsEntityRegistrationScenario() : base(new TestScenarioDefinition(
            "physics-entity-registration",
            "Physics Entity Registration (Visual)",
            "Spawns colored cubes, registers them as physics entities, then unregisters the last one. " +
            "Registration state is logged to the Console.",
            new[] { EntityCountParam, SpacingParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var entityCount = Mathf.Clamp(ResolveParam<int>(overrides, "entityCount"), 1, 6);
            var spacing = ResolveParam<float>(overrides, "spacing");

            _entities.Clear();
            _unregisteredId = PhysicsEntityId.None;
            _unregisterLookupIsNone = false;
            _unregisterTryGetFails = false;
            _idempotentReRegisterWorks = false;

            _registry = new PhysicsEntityRegistry();

            // --- Spawn and register cubes ---
            var startX = -(entityCount - 1) * spacing * 0.5f;
            for (int i = 0; i < entityCount; i++)
            {
                var tag = EntityTags[i % EntityTags.Length];
                var color = EntityColors[i % EntityColors.Length];

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"[Scenario] Entity '{tag}'";
                cube.transform.SetParent(SceneRoot.transform);
                cube.transform.localPosition = new Vector3(startX + i * spacing, 0.5f, 0f);
                SetColor(cube, color);

                var id = _registry.Register(cube, tag);
                _entities.Add((cube, id, tag));

                Debug.Log($"[PhysicsScenario] Registered: tag='{tag}' id={id} go={cube.name}");
            }

            // --- Test idempotent re-register ---
            if (_entities.Count > 0)
            {
                var first = _entities[0];
                var reId = _registry.Register(first.Go, first.Tag);
                _idempotentReRegisterWorks = reId == first.Id;
                Debug.Log($"[PhysicsScenario] Idempotent re-register: original={first.Id} re-registered={reId} " +
                          $"match={_idempotentReRegisterWorks}");
            }

            // --- Unregister the last entity ---
            if (_entities.Count > 1)
            {
                var last = _entities[^1];
                _unregisteredId = last.Id;
                _registry.Unregister(last.Id);

                _unregisterLookupIsNone = _registry.GetIdForGameObject(last.Go) == PhysicsEntityId.None;
                _unregisterTryGetFails = !_registry.TryGetGameObject(last.Id, out _);

                // Dim the unregistered cube
                SetColor(last.Go, new Color(0.3f, 0.3f, 0.3f, 0.5f));

                Debug.Log($"[PhysicsScenario] Unregistered: tag='{last.Tag}' id={last.Id} " +
                          $"lookupNone={_unregisterLookupIsNone} tryGetFails={_unregisterTryGetFails}");
            }

            Debug.Log($"[PhysicsScenario] Registration scenario complete — " +
                      $"{entityCount} spawned, {(_entities.Count > 1 ? 1 : 0)} unregistered");
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var entityCount = Mathf.Clamp(ResolveParam<int>(overrides, "entityCount"), 1, 6);

            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root created", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new($"All {entityCount} cubes spawned", _entities.Count == entityCount,
                    _entities.Count == entityCount ? null : $"Expected {entityCount}, got {_entities.Count}"),
                new("All entity IDs are valid",
                    _entities.TrueForAll(e => e.Id.IsValid),
                    _entities.TrueForAll(e => e.Id.IsValid) ? null : "Some IDs were None"),
                new("Idempotent re-register returns same ID", _idempotentReRegisterWorks,
                    _idempotentReRegisterWorks ? null : "Re-register returned different ID")
            };

            if (_entities.Count > 1)
            {
                checks.Add(new("After unregister, lookup returns None", _unregisterLookupIsNone,
                    _unregisterLookupIsNone ? null : "Lookup still returned valid ID"));
                checks.Add(new("After unregister, TryGetGameObject fails", _unregisterTryGetFails,
                    _unregisterTryGetFails ? null : "TryGetGameObject still succeeded"));
            }

            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup()
        {
            _entities.Clear();
            _registry = null;
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
