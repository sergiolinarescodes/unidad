using System.Collections.Generic;
using Unidad.Core.Testing;
using UnityEngine;

namespace Unidad.Core.Physics2D.Scenarios
{
    /// <summary>
    /// Visual scenario: spawns colored sprites, registers them as 2D physics entities,
    /// then unregisters the last one. Registration state is logged to the Console.
    /// </summary>
    internal sealed class Physics2DEntityRegistrationScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter EntityCountParam = new(
            "entityCount", "Entity Count", typeof(int), 3, 1, 6);

        private static readonly ScenarioParameter SpacingParam = new(
            "spacing", "Spacing Between Sprites", typeof(float), 2.5f, 1f, 5f);

        private Physics2DEntityRegistry _registry;
        private readonly List<(GameObject Go, Physics2DEntityId Id, string Tag)> _entities = new();
        private Physics2DEntityId _unregisteredId;
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

        public Physics2DEntityRegistrationScenario() : base(new TestScenarioDefinition(
            "physics2d-entity-registration",
            "Physics2D Entity Registration (Visual)",
            "Spawns colored sprites, registers them as 2D physics entities, then unregisters the last one. " +
            "Registration state is logged to the Console.",
            new[] { EntityCountParam, SpacingParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var entityCount = Mathf.Clamp(ResolveParam<int>(overrides, "entityCount"), 1, 6);
            var spacing = ResolveParam<float>(overrides, "spacing");

            _entities.Clear();
            _unregisteredId = Physics2DEntityId.None;
            _unregisterLookupIsNone = false;
            _unregisterTryGetFails = false;
            _idempotentReRegisterWorks = false;

            _registry = new Physics2DEntityRegistry();

            var startX = -(entityCount - 1) * spacing * 0.5f;
            for (int i = 0; i < entityCount; i++)
            {
                var tag = EntityTags[i % EntityTags.Length];
                var color = EntityColors[i % EntityColors.Length];

                var sprite = CreateColoredSprite($"[Scenario] Entity '{tag}'", color);
                sprite.transform.SetParent(SceneRoot.transform);
                sprite.transform.localPosition = new Vector3(startX + i * spacing, 0.5f, 0f);

                var id = _registry.Register(sprite, tag);
                _entities.Add((sprite, id, tag));

                Debug.Log($"[Physics2DScenario] Registered: tag='{tag}' id={id} go={sprite.name}");
            }

            if (_entities.Count > 0)
            {
                var first = _entities[0];
                var reId = _registry.Register(first.Go, first.Tag);
                _idempotentReRegisterWorks = reId == first.Id;
                Debug.Log($"[Physics2DScenario] Idempotent re-register: original={first.Id} re-registered={reId} " +
                          $"match={_idempotentReRegisterWorks}");
            }

            if (_entities.Count > 1)
            {
                var last = _entities[^1];
                _unregisteredId = last.Id;
                _registry.Unregister(last.Id);

                _unregisterLookupIsNone = _registry.GetIdForGameObject(last.Go) == Physics2DEntityId.None;
                _unregisterTryGetFails = !_registry.TryGetGameObject(last.Id, out _);

                SetSpriteColor(last.Go, new Color(0.3f, 0.3f, 0.3f, 0.5f));

                Debug.Log($"[Physics2DScenario] Unregistered: tag='{last.Tag}' id={last.Id} " +
                          $"lookupNone={_unregisterLookupIsNone} tryGetFails={_unregisterTryGetFails}");
            }

            Debug.Log($"[Physics2DScenario] Registration scenario complete — " +
                      $"{entityCount} spawned, {(_entities.Count > 1 ? 1 : 0)} unregistered");
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var entityCount = Mathf.Clamp(ResolveParam<int>(overrides, "entityCount"), 1, 6);

            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root created", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new($"All {entityCount} sprites spawned", _entities.Count == entityCount,
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

        private static void SetSpriteColor(GameObject go, Color color)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            sr.color = color;
        }
    }
}
