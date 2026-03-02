using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;
using UnityEngine;

namespace Unidad.Core.Timer.Scenarios
{
    /// <summary>
    /// Visual scenario: spawns a cube that changes color as a timer progresses.
    /// On loop, the color resets. Timer events are logged to the Console.
    /// </summary>
    internal sealed class TimerScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter DurationParam = new(
            "duration", "Duration (s)", typeof(float), 3f, 0.5f, 10f);

        private static readonly ScenarioParameter LoopParam = new(
            "loop", "Loop", typeof(bool), true);

        private IEventBus _eventBus;
        private TimerService _timerService;
        private TimerHandle _handle;
        private readonly List<IDisposable> _subscriptions = new();
        private GameObject _indicator;
        private Renderer _indicatorRenderer;
        private Material _indicatorMaterial;
        private int _completionCount;

        public TimerScenario() : base(new TestScenarioDefinition(
            "timer-progress",
            "Timer Progress (Live)",
            "Spawns a cube that transitions from green to red as the timer progresses. " +
            "On loop, the color resets to green. Timer events are logged to the Console.",
            new[] { DurationParam, LoopParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var duration = ResolveParam<float>(overrides, "duration");
            var loop = ResolveParam<bool>(overrides, "loop");

            _completionCount = 0;

            // --- Services ---
            _eventBus = new EventBus.EventBus();
            _timerService = new TimerService(_eventBus);

            // --- Visual indicator ---
            _indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _indicator.name = "[Scenario] Timer Indicator";
            _indicator.transform.SetParent(SceneRoot.transform);
            _indicator.transform.localPosition = Vector3.zero;
            _indicator.transform.localScale = new Vector3(2f, 2f, 2f);

            _indicatorRenderer = _indicator.GetComponent<Renderer>();
            _indicatorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _indicatorMaterial.color = Color.green;
            _indicatorRenderer.sharedMaterial = _indicatorMaterial;

            // --- Subscribe to timer events ---
            _subscriptions.Add(_eventBus.Subscribe<TimerCompletedEvent>(evt =>
            {
                _completionCount++;
                Debug.Log($"[TimerScenario] Timer completed (count={_completionCount}) handle={evt.Handle.Id}");
            }));

            _subscriptions.Add(_eventBus.Subscribe<TimerCancelledEvent>(evt =>
            {
                Debug.Log($"[TimerScenario] Timer cancelled handle={evt.Handle.Id}");
            }));

            // --- Start timer ---
            _handle = _timerService.Start(duration, null, loop);

            Debug.Log($"[TimerScenario] Started — duration={duration} loop={loop} handle={_handle.Id}");

            // --- Attach updater for visual feedback ---
            var updater = SceneRoot.AddComponent<TimerScenarioUpdater>();
            updater.Initialize(_timerService, _handle, _indicatorMaterial);
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root created", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Indicator spawned", _indicator != null,
                    _indicator != null ? null : "Indicator GameObject is null"),
                new("Indicator has Renderer", _indicatorRenderer != null,
                    _indicatorRenderer != null ? null : "Missing Renderer"),
                new("Timer handle is valid", _handle.IsValid,
                    _handle.IsValid ? null : "Timer handle is None"),
                new("Timer service created", _timerService != null,
                    _timerService != null ? null : "Timer service is null")
            };
            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup()
        {
            foreach (var sub in _subscriptions) sub.Dispose();
            _subscriptions.Clear();

            if (_indicatorMaterial != null)
                UnityEngine.Object.DestroyImmediate(_indicatorMaterial);

            _eventBus?.ClearAllSubscriptions();
            _eventBus = null;
            _timerService = null;
            _indicator = null;
            _indicatorRenderer = null;
            _indicatorMaterial = null;
        }
    }

    /// <summary>
    /// MonoBehaviour that ticks the timer service and updates indicator color each frame.
    /// Only used by TimerScenario for visual feedback.
    /// </summary>
    internal sealed class TimerScenarioUpdater : MonoBehaviour
    {
        private TimerService _timerService;
        private TimerHandle _handle;
        private Material _material;

        public void Initialize(TimerService timerService, TimerHandle handle, Material material)
        {
            _timerService = timerService;
            _handle = handle;
            _material = material;
        }

        private void Update()
        {
            if (_timerService == null || _material == null) return;

            _timerService.Tick(Time.deltaTime);

            var progress = _timerService.GetProgress(_handle);
            _material.color = Color.Lerp(Color.green, Color.red, progress);
        }
    }
}
