using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;
using UnityEngine;

namespace Unidad.Core.Resource.Scenarios
{
    /// <summary>
    /// Visual scenario: spawns a progress bar (quad) that fills/depletes over time.
    /// Resource events are logged to the Console.
    /// </summary>
    internal sealed class ResourceScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter MaxValueParam = new(
            "maxValue", "Max Value", typeof(float), 100f, 10f, 500f);

        private static readonly ScenarioParameter InitialValueParam = new(
            "initialValue", "Initial Value", typeof(float), 100f, 0f, 500f);

        private static readonly ScenarioParameter SpendRateParam = new(
            "spendRate", "Spend Rate /s", typeof(float), 15f, 1f, 100f);

        private IEventBus _eventBus;
        private ResourceService _resourceService;
        private readonly List<IDisposable> _subscriptions = new();
        private GameObject _barBackground;
        private GameObject _barFill;
        private Material _fillMaterial;
        private Material _bgMaterial;
        private ResourceId _energyId;
        private float _maxValue;

        public ResourceScenario() : base(new TestScenarioDefinition(
            "resource-bar",
            "Resource Bar (Live)",
            "Spawns a progress bar that depletes over time at a configurable rate. " +
            "Resource events are logged to the Console.",
            new[] { MaxValueParam, InitialValueParam, SpendRateParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            _maxValue = ResolveParam<float>(overrides, "maxValue");
            var initialValue = Mathf.Clamp(ResolveParam<float>(overrides, "initialValue"), 0f, _maxValue);
            var spendRate = ResolveParam<float>(overrides, "spendRate");

            _energyId = new ResourceId("energy");

            // --- Services ---
            _eventBus = new EventBus.EventBus();
            _resourceService = new ResourceService(_eventBus);
            _resourceService.Define(_energyId, new ResourceDefinition(initialValue, 0f, _maxValue));

            // --- Subscribe ---
            _subscriptions.Add(_eventBus.Subscribe<ResourceChangedEvent>(evt =>
            {
                Debug.Log($"[ResourceScenario] {evt.Id}: {evt.OldValue:F1} -> {evt.NewValue:F1} (max={evt.Max:F1})");
            }));
            _subscriptions.Add(_eventBus.Subscribe<ResourceDepletedEvent>(evt =>
            {
                Debug.Log($"[ResourceScenario] {evt.Id} DEPLETED!");
            }));
            _subscriptions.Add(_eventBus.Subscribe<ResourceFilledEvent>(evt =>
            {
                Debug.Log($"[ResourceScenario] {evt.Id} FILLED!");
            }));

            // --- Visual bar ---
            var barWidth = 4f;
            var barHeight = 0.5f;

            _barBackground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _barBackground.name = "[Scenario] Bar Background";
            _barBackground.transform.SetParent(SceneRoot.transform);
            _barBackground.transform.localPosition = Vector3.zero;
            _barBackground.transform.localScale = new Vector3(barWidth, barHeight, 1f);
            _bgMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _bgMaterial.color = new Color(0.2f, 0.2f, 0.2f);
            _barBackground.GetComponent<Renderer>().sharedMaterial = _bgMaterial;

            _barFill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _barFill.name = "[Scenario] Bar Fill";
            _barFill.transform.SetParent(SceneRoot.transform);
            _barFill.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            _barFill.transform.localScale = new Vector3(barWidth, barHeight, 1f);
            _fillMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _fillMaterial.color = Color.green;
            _barFill.GetComponent<Renderer>().sharedMaterial = _fillMaterial;

            // --- Attach updater ---
            var updater = SceneRoot.AddComponent<ResourceScenarioUpdater>();
            updater.Initialize(_resourceService, _energyId, _maxValue, spendRate, barWidth, _barFill, _fillMaterial);

            Debug.Log($"[ResourceScenario] Started — max={_maxValue} initial={initialValue} spendRate={spendRate}/s");
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root created", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Resource service created", _resourceService != null,
                    _resourceService != null ? null : "Resource service is null"),
                new("Energy resource defined", _resourceService != null && _resourceService.Has(_energyId),
                    _resourceService != null && _resourceService.Has(_energyId)
                        ? null : "Energy resource not defined"),
                new("Bar background spawned", _barBackground != null,
                    _barBackground != null ? null : "Bar background is null"),
                new("Bar fill spawned", _barFill != null,
                    _barFill != null ? null : "Bar fill is null")
            };
            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup()
        {
            foreach (var sub in _subscriptions) sub.Dispose();
            _subscriptions.Clear();

            if (_fillMaterial != null) UnityEngine.Object.DestroyImmediate(_fillMaterial);
            if (_bgMaterial != null) UnityEngine.Object.DestroyImmediate(_bgMaterial);

            _eventBus?.ClearAllSubscriptions();
            _eventBus = null;
            _resourceService = null;
            _barBackground = null;
            _barFill = null;
            _fillMaterial = null;
            _bgMaterial = null;
        }
    }

    /// <summary>
    /// MonoBehaviour that spends resource each frame and updates the visual bar.
    /// </summary>
    internal sealed class ResourceScenarioUpdater : MonoBehaviour
    {
        private ResourceService _resourceService;
        private ResourceId _id;
        private float _maxValue;
        private float _spendRate;
        private float _barWidth;
        private GameObject _barFill;
        private Material _fillMaterial;

        public void Initialize(ResourceService service, ResourceId id, float maxValue,
            float spendRate, float barWidth, GameObject barFill, Material fillMaterial)
        {
            _resourceService = service;
            _id = id;
            _maxValue = maxValue;
            _spendRate = spendRate;
            _barWidth = barWidth;
            _barFill = barFill;
            _fillMaterial = fillMaterial;
        }

        private void Update()
        {
            if (_resourceService == null || _barFill == null) return;

            _resourceService.TrySpend(_id, _spendRate * Time.deltaTime);

            var current = _resourceService.Get(_id);
            var max = _resourceService.GetMax(_id);
            var ratio = max > 0f ? current / max : 0f;

            _barFill.transform.localScale = new Vector3(_barWidth * ratio, 0.5f, 1f);
            _barFill.transform.localPosition = new Vector3(
                -_barWidth * (1f - ratio) * 0.5f, 0f, -0.01f);

            if (_fillMaterial != null)
                _fillMaterial.color = Color.Lerp(Color.red, Color.green, ratio);
        }
    }
}
