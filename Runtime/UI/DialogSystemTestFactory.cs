using System;
using System.Collections.Generic;
using Unidad.Core.Testing;
using Unidad.Core.UI.Components;
using Unidad.Core.UI.DesignSystem;
using Unidad.Core.UI.Dialog;
using Unidad.Core.UI.Events;
using Unidad.Core.UI.TextAnimation;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using UnityEngine;
using UnityEngine.UIElements;
using IEventBus = Unidad.Core.EventBus.IEventBus;

namespace Unidad.Core.UI
{
    internal sealed class DialogSystemTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[] { typeof(IDialogService) };

        public object CreateForTesting(TestDependencies deps)
        {
            var textAnimation = new TextAnimationService();
            var elementAnimator = new ElementAnimator();
            return new DialogService(deps.EventBus, textAnimation, elementAnimator, null);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new DialogQueueScenario();
            yield return new DialogDismissScenario();
        }
    }

    internal sealed class DialogQueueScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter DialogCountParam = new(
            "dialogCount", "Dialog Count", typeof(int), 2, 1, 5);

        private static readonly ScenarioParameter TitleParam = new(
            "title", "Dialog Title", typeof(string), "Quest Update");

        private static readonly ScenarioParameter BodyTextParam = new(
            "bodyText", "Body Text (supports animation tags)", typeof(string),
            "You found a <wave>legendary sword</wave>! This is <shake>incredible</shake>.");

        private static readonly ScenarioParameter TypewriterSpeedParam = new(
            "typewriterSpeed", "Typewriter Speed (chars/sec)", typeof(float), 30f, 5f, 100f);

        private static readonly ScenarioParameter SkipOnClickParam = new(
            "skipOnClick", "Skip Typewriter On Click", typeof(bool), true);

        private DialogService _service;
        private IEventBus _eventBus;
        private int _dialogShownCount;

        public DialogQueueScenario() : base(new TestScenarioDefinition(
            "dialog-queue",
            "Dialog Queue",
            "Shows queued dialogs with typewriter text. Click to skip typewriter, click buttons to dismiss and see next dialog.",
            new[] { DialogCountParam, TitleParam, BodyTextParam, TypewriterSpeedParam, SkipOnClickParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var dialogCount = ResolveParam<int>(overrides, "dialogCount");
            var title = ResolveParam<string>(overrides, "title");
            var bodyText = ResolveParam<string>(overrides, "bodyText");
            var typewriterSpeed = ResolveParam<float>(overrides, "typewriterSpeed");
            var skipOnClick = ResolveParam<bool>(overrides, "skipOnClick");

            _dialogShownCount = 0;
            _eventBus = new Unidad.Core.EventBus.EventBus();

            // Build text animation service with custom speed
            var textAnimation = new TextAnimationService();
            var dialogPreset = TextAnimationPresets.CreateDialogPreset();
            dialogPreset.baseAppearanceSpeed = typewriterSpeed;
            textAnimation.RegisterPreset("dialog", dialogPreset);

            var elementAnimator = new ElementAnimator();

            var root = RootVisualElement;

            var dialogLayer = new VisualElement
            {
                name = "dialog-layer",
                pickingMode = PickingMode.Ignore
            };
            dialogLayer.style.position = Position.Absolute;
            dialogLayer.style.left = 0;
            dialogLayer.style.right = 0;
            dialogLayer.style.top = 0;
            dialogLayer.style.bottom = 0;
            root.Add(dialogLayer);

            _service = new DialogService(_eventBus, textAnimation, elementAnimator, dialogLayer);
            _eventBus.Subscribe<DialogShownEvent>(_ => _dialogShownCount++);

            for (var i = 0; i < dialogCount; i++)
            {
                var index = i + 1;
                var def = new DialogDefinition(
                    $"{title} ({index}/{dialogCount})",
                    bodyText,
                    new[]
                    {
                        new DialogButton("Dismiss", $"dismiss-{index}", ButtonVariant.Secondary),
                        new DialogButton("Confirm", $"confirm-{index}", ButtonVariant.Primary)
                    },
                    id: $"dialog-{index}",
                    animationPreset: "dialog",
                    skipOnClick: skipOnClick
                );
                _service.Show(def);
            }
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("UIDocument created in scene", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Has active dialog", _service.HasActiveDialog,
                    _service.HasActiveDialog ? null : "No active dialog"),
                new("First dialog shown immediately", _dialogShownCount >= 1,
                    _dialogShownCount >= 1 ? null : $"Shown count: {_dialogShownCount}")
            };
            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup() => _service?.Dispose();
    }

    internal sealed class DialogDismissScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter TitleParam = new(
            "title", "Dialog Title", typeof(string), "Auto-Dismiss Test");

        private static readonly ScenarioParameter BodyParam = new(
            "body", "Body Text", typeof(string), "This dialog will dismiss itself immediately.");

        private DialogService _service;
        private bool _resultReceived;
        private string _resultButtonId;

        public DialogDismissScenario() : base(new TestScenarioDefinition(
            "dialog-dismiss",
            "Dialog Dismiss",
            "Shows a dialog and immediately dismisses it. Verifies callback fires with correct result.",
            new[] { TitleParam, BodyParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var title = ResolveParam<string>(overrides, "title");
            var body = ResolveParam<string>(overrides, "body");

            _resultReceived = false;
            _resultButtonId = null;

            var eventBus = new Unidad.Core.EventBus.EventBus();
            var textAnimation = new TextAnimationService();
            var elementAnimator = new ElementAnimator();

            var root = RootVisualElement;

            var dialogLayer = new VisualElement { name = "dialog-layer" };
            dialogLayer.style.position = Position.Absolute;
            dialogLayer.style.left = 0;
            dialogLayer.style.right = 0;
            dialogLayer.style.top = 0;
            dialogLayer.style.bottom = 0;
            root.Add(dialogLayer);

            _service = new DialogService(eventBus, textAnimation, elementAnimator, dialogLayer);

            var def = new DialogDefinition(title, body,
                new[] { new DialogButton("OK") }, id: "test-dialog");

            _service.Show(def, result =>
            {
                _resultReceived = true;
                _resultButtonId = result.ButtonId;
            });

            _service.DismissCurrent();
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Result callback invoked", _resultReceived,
                    _resultReceived ? null : "Callback was not invoked"),
                new("Button ID is 'dismissed'", _resultButtonId == "dismissed",
                    _resultButtonId == "dismissed" ? null : $"Expected 'dismissed', got '{_resultButtonId}'"),
                new("No active dialog after dismiss", !_service.HasActiveDialog,
                    !_service.HasActiveDialog ? null : "Dialog still active")
            };
            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup() => _service?.Dispose();
    }
}
