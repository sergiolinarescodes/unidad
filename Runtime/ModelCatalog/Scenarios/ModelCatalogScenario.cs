using UnityEngine;
using Unidad.Core.Abstractions;
using Unidad.Core.EventBus;
using EventBus = Unidad.Core.EventBus.EventBus;
using Unidad.Core.Testing;

namespace Unidad.Core.ModelCatalog.Scenarios
{
    /// <summary>
    /// Visual scenario: spawns a pipeline-imported model prefab from the catalog,
    /// plays its baked Animator clip and a kind effect. Animator/tweens only move
    /// in play mode; the spawned model itself is visible in any mode.
    /// </summary>
    public sealed class ModelCatalogScenario : DataDrivenScenario
    {
        EventBus _eventBus;
        LocalGameObjectFactory _factory;
        ModelCatalogService _service;
        ModelInstance _instance;
        readonly System.Collections.Generic.List<System.IDisposable> _subscriptions = new();

        public ModelCatalogScenario() : base(new TestScenarioDefinition(
            "modelcatalog.spawn",
            "Model Catalog — Spawn Imported Model",
            "Spawns a picoCAD-pipeline model from Resources via the catalog service, " +
            "plays its baked clip and a kind effect. Logs to Console with [ModelCatalogScenario].",
            new[]
            {
                new ScenarioParameter("modelId", "Model Id", typeof(string), "pig"),
            }))
        {
        }

        string _skipReason;

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var modelId = ResolveParam<string>(overrides, "modelId");

            // Fixture guard: imported model prefabs are project content — a clean
            // package clone (or a project that hasn't run the pipeline yet) has none.
            var database = ModelCatalogDatabase.LoadFromResources();
            ModelEntry entry = null;
            foreach (var model in database.Models)
                if (model.id == modelId) { entry = model; break; }
            if (entry == null)
            {
                _skipReason = $"model '{modelId}' not registered in Resources/ModelCatalog/models.json — run /picocad-model first";
                return;
            }
            if (Resources.Load<GameObject>(entry.prefabPath) == null)
            {
                _skipReason = $"prefab missing at Resources/{entry.prefabPath} — run /picocad-model first";
                return;
            }

            _eventBus = new EventBus();
            _factory = new LocalGameObjectFactory();

            IAnimationResolver resolver;
            if (Application.isPlaying)
            {
                var primeResolver = new PrimeTweenAnimationResolver();
                primeResolver.RegisterProfile(new Effects.BounceEffectProfile());
                resolver = primeResolver;
                _service = new ModelCatalogService(_eventBus, _factory, resolver,
                    ModelCatalogDatabase.LoadFromResources());
                primeResolver.InstanceResolver = _service.ResolveInstanceForEffects;
            }
            else
            {
                resolver = new InstantAnimationResolver();
                _service = new ModelCatalogService(_eventBus, _factory, resolver,
                    ModelCatalogDatabase.LoadFromResources());
            }

            _subscriptions.Add(_eventBus.Subscribe<ModelSpawnedEvent>(e =>
                Debug.Log($"[ModelCatalogScenario] spawned {e.InstanceId} (model={e.ModelId}, kind={e.KindId})")));
            _subscriptions.Add(_eventBus.Subscribe<ModelEffectPlayedEvent>(e =>
                Debug.Log($"[ModelCatalogScenario] effect {e.EffectId} on {e.InstanceId}")));

            _instance = _service.Spawn(modelId, Vector3.zero);
            _instance.View.Root.transform.SetParent(SceneRoot.transform, true);

            // baked picoCAD motion (loops in play mode)
            _instance.View.PlayClip("Motion");

            // kind effect through the resolver (instant outside play mode)
            _service.PlayEffect(_instance.InstanceId, "hop",
                () => Debug.Log("[ModelCatalogScenario] hop complete"));
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            if (_skipReason != null)
                return ScenarioVerificationResult.Skip(_skipReason);

            var checks = new System.Collections.Generic.List<ScenarioVerificationResult.CheckResult>
            {
                new("service created", _service != null, null),
                new("instance spawned", _instance != null, "Spawn returned null"),
                new("root GameObject exists", _instance?.View.Root != null, "no GameObject"),
                new("renderer present", _instance?.View.Root != null &&
                    _instance.View.Root.GetComponentInChildren<Renderer>() != null, "no Renderer in children"),
                new("instance registered", _service != null && _service.InstanceCount == 1,
                    $"expected 1 instance, got {_service?.InstanceCount ?? 0}"),
            };
            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
            _service?.Dispose();
            _factory?.Dispose();
            _eventBus?.ClearAllSubscriptions();
            _service = null;
            _instance = null;
            _factory = null;
            _eventBus = null;
            _skipReason = null;
        }
    }
}
