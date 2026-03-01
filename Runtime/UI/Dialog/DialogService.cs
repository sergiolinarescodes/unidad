using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;
using Unidad.Core.UI.Components;
using Unidad.Core.UI.Events;
using Unidad.Core.UI.TextAnimation;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using TextAnimationsForUIToolkit;
using TextAnimationsForUIToolkit.Events;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Dialog
{
    internal sealed class DialogService : SystemServiceBase, IDialogService
    {
        private readonly ITextAnimationService _textAnimationService;
        private readonly IElementAnimator _elementAnimator;
        private readonly VisualElement _dialogLayer;
        private readonly Queue<PendingDialog> _queue = new();
        private PendingDialog _current;

        public bool HasActiveDialog => _current != null;

        public DialogService(
            IEventBus eventBus,
            ITextAnimationService textAnimationService,
            IElementAnimator elementAnimator,
            VisualElement dialogLayer) : base(eventBus)
        {
            _textAnimationService = textAnimationService;
            _elementAnimator = elementAnimator;
            _dialogLayer = dialogLayer;
        }

        public void Show(DialogDefinition definition, Action<DialogResult> onResult = null)
        {
            var pending = new PendingDialog(definition, onResult);

            if (_current != null)
            {
                _queue.Enqueue(pending);
                return;
            }

            ShowInternal(pending);
        }

        public void ShowConfirm(string title, string message, Action<DialogResult> onResult = null)
        {
            var definition = new DialogDefinition(
                title,
                message,
                new[]
                {
                    new DialogButton("Cancel", "cancel", ButtonVariant.Secondary),
                    new DialogButton("Confirm", "confirm", ButtonVariant.Primary)
                });
            Show(definition, onResult);
        }

        public void ShowAlert(string title, string message, Action onDismiss = null)
        {
            var definition = new DialogDefinition(
                title,
                message,
                new[] { new DialogButton("OK", "ok") });
            Show(definition, onDismiss != null ? _ => onDismiss() : null);
        }

        public void DismissCurrent()
        {
            if (_current == null) return;
            DismissInternal(DialogResult.Dismissed);
        }

        public void DismissAll()
        {
            _queue.Clear();
            DismissCurrent();
        }

        private void ShowInternal(PendingDialog pending)
        {
            _current = pending;
            var definition = pending.Definition;

            // Build dialog visual tree
            var backdrop = new VisualElement();
            backdrop.AddToClassList("unidad-dialog-backdrop");

            var dialog = new VisualElement();
            dialog.AddToClassList("unidad-dialog");

            // Title
            var titleLabel = new UnidadLabel(definition.Title);
            titleLabel.AddToClassList("unidad-dialog__title");
            dialog.Add(titleLabel);

            // Body with text animation
            var bodyLabel = _textAnimationService.CreateLabel(definition.AnimationPreset);
            bodyLabel.AddToClassList("unidad-dialog__body");
            dialog.Add(bodyLabel);

            // Buttons container (hidden until typewriter completes)
            var buttonsContainer = new VisualElement();
            buttonsContainer.AddToClassList("unidad-dialog__buttons");
            buttonsContainer.style.opacity = 0;
            dialog.Add(buttonsContainer);

            foreach (var btnDef in definition.Buttons)
            {
                var button = new UnidadButton(btnDef.Label, () =>
                {
                    DismissInternal(new DialogResult(btnDef.Id, definition.Id));
                });
                button.SetVariant(btnDef.Variant);
                buttonsContainer.Add(button);
            }

            backdrop.Add(dialog);
            _current.Root = backdrop;
            _current.BodyLabel = bodyLabel;

            // Skip typewriter on click
            if (definition.SkipOnClick)
            {
                backdrop.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (bodyLabel.isAppearing)
                    {
                        _textAnimationService.Skip(bodyLabel);
                        evt.StopPropagation();
                    }
                });
            }

            // Show buttons after typewriter completes
            void OnTypewriterFinished(TextAppearanceFinishedEvent _)
            {
                bodyLabel.textAppearanceFinished -= OnTypewriterFinished;
                pending.OnTypewriterFinished = null;
                _elementAnimator.Animate(buttonsContainer, new ElementAnimationConfig(ElementAnimationType.FadeIn, 0.2f));
            }

            bodyLabel.textAppearanceFinished += OnTypewriterFinished;
            pending.OnTypewriterFinished = OnTypewriterFinished;

            // If text appearance is disabled, show buttons immediately
            var settings = _textAnimationService.GetPreset(definition.AnimationPreset);
            if (settings == null || !settings.enableTextAppearance)
            {
                buttonsContainer.style.opacity = 1;
            }

            _dialogLayer?.Add(backdrop);

            // Animate dialog in
            _elementAnimator.Animate(dialog, new ElementAnimationConfig(ElementAnimationType.ScaleIn, 0.3f));

            // Start typewriter
            _textAnimationService.PlayTypewriter(bodyLabel, definition.Body);

            Publish(new DialogShownEvent(definition.Id));
        }

        private void DismissInternal(DialogResult result)
        {
            if (_current == null) return;

            var pending = _current;
            _current = null;

            // Unsubscribe typewriter callback if dialog dismissed before typewriter finished
            if (pending.OnTypewriterFinished != null && pending.BodyLabel != null)
                pending.BodyLabel.textAppearanceFinished -= pending.OnTypewriterFinished;

            pending.Root?.RemoveFromHierarchy();
            pending.OnResult?.Invoke(result);

            Publish(new DialogDismissedEvent(pending.Definition.Id, result));

            // Show next in queue
            if (_queue.Count > 0)
                ShowInternal(_queue.Dequeue());
        }

        public override void Dispose()
        {
            DismissAll();
            base.Dispose();
        }

        private sealed class PendingDialog
        {
            public DialogDefinition Definition { get; }
            public Action<DialogResult> OnResult { get; }
            public VisualElement Root { get; set; }
            public AnimatedLabel BodyLabel { get; set; }
            public Action<TextAppearanceFinishedEvent> OnTypewriterFinished { get; set; }

            public PendingDialog(DialogDefinition definition, Action<DialogResult> onResult)
            {
                Definition = definition;
                OnResult = onResult;
            }
        }
    }
}
