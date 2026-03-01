using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.TextAnimation.ElementAnimation
{
    internal sealed class ElementAnimator : IElementAnimator
    {
        private readonly Dictionary<VisualElement, IVisualElementScheduledItem> _activeAnimations = new();

        public IDisposable Animate(VisualElement target, ElementAnimationConfig config, Action onComplete = null)
        {
            Cancel(target);

            SetupInitialState(target, config);

            var scheduledItem = target.schedule.Execute(() =>
            {
                ApplyFinalState(target, config);
            });

            var completionItem = target.schedule.Execute(() =>
            {
                _activeAnimations.Remove(target);
                onComplete?.Invoke();
            }).StartingIn((long)(config.Duration * 1000));

            _activeAnimations[target] = completionItem;

            return new ActionDisposable(() => Cancel(target));
        }

        public void Cancel(VisualElement target)
        {
            if (_activeAnimations.TryGetValue(target, out var scheduled))
            {
                scheduled.Pause();
                _activeAnimations.Remove(target);
            }
        }

        private static void SetupInitialState(VisualElement target, ElementAnimationConfig config)
        {
            var durationMs = (int)(config.Duration * 1000);
            var easing = ToEasingFunction(config.Easing);

            switch (config.Type)
            {
                case ElementAnimationType.SlideIn:
                    SetSlideOffset(target, config.Direction, true);
                    target.style.transitionProperty = new List<StylePropertyName> { new("translate") };
                    target.style.transitionDuration = new List<TimeValue> { new(durationMs, TimeUnit.Millisecond) };
                    target.style.transitionTimingFunction = new List<EasingFunction> { easing };
                    break;

                case ElementAnimationType.SlideOut:
                    target.style.translate = new Translate(0, 0);
                    target.style.transitionProperty = new List<StylePropertyName> { new("translate") };
                    target.style.transitionDuration = new List<TimeValue> { new(durationMs, TimeUnit.Millisecond) };
                    target.style.transitionTimingFunction = new List<EasingFunction> { easing };
                    break;

                case ElementAnimationType.FadeIn:
                    target.style.opacity = 0;
                    target.style.transitionProperty = new List<StylePropertyName> { new("opacity") };
                    target.style.transitionDuration = new List<TimeValue> { new(durationMs, TimeUnit.Millisecond) };
                    target.style.transitionTimingFunction = new List<EasingFunction> { easing };
                    break;

                case ElementAnimationType.FadeOut:
                    target.style.opacity = 1;
                    target.style.transitionProperty = new List<StylePropertyName> { new("opacity") };
                    target.style.transitionDuration = new List<TimeValue> { new(durationMs, TimeUnit.Millisecond) };
                    target.style.transitionTimingFunction = new List<EasingFunction> { easing };
                    break;

                case ElementAnimationType.ScaleIn:
                    target.style.scale = new Scale(new Vector2(0, 0));
                    target.style.transitionProperty = new List<StylePropertyName> { new("scale") };
                    target.style.transitionDuration = new List<TimeValue> { new(durationMs, TimeUnit.Millisecond) };
                    target.style.transitionTimingFunction = new List<EasingFunction> { easing };
                    break;

                case ElementAnimationType.ScaleOut:
                    target.style.scale = new Scale(new Vector2(1, 1));
                    target.style.transitionProperty = new List<StylePropertyName> { new("scale") };
                    target.style.transitionDuration = new List<TimeValue> { new(durationMs, TimeUnit.Millisecond) };
                    target.style.transitionTimingFunction = new List<EasingFunction> { easing };
                    break;

                case ElementAnimationType.Shake:
                    target.style.translate = new Translate(0, 0);
                    break;
            }
        }

        private static void ApplyFinalState(VisualElement target, ElementAnimationConfig config)
        {
            switch (config.Type)
            {
                case ElementAnimationType.SlideIn:
                    target.style.translate = new Translate(0, 0);
                    break;
                case ElementAnimationType.SlideOut:
                    SetSlideOffset(target, config.Direction, true);
                    break;
                case ElementAnimationType.FadeIn:
                    target.style.opacity = 1;
                    break;
                case ElementAnimationType.FadeOut:
                    target.style.opacity = 0;
                    break;
                case ElementAnimationType.ScaleIn:
                    target.style.scale = new Scale(new Vector2(1, 1));
                    break;
                case ElementAnimationType.ScaleOut:
                    target.style.scale = new Scale(new Vector2(0, 0));
                    break;
                case ElementAnimationType.Shake:
                    RunShake(target, config);
                    break;
            }
        }

        private static void SetSlideOffset(VisualElement target, SlideDirection direction, bool apply)
        {
            var offset = direction switch
            {
                SlideDirection.Left => new Translate(Length.Percent(-100), 0),
                SlideDirection.Right => new Translate(Length.Percent(100), 0),
                SlideDirection.Top => new Translate(0, Length.Percent(-100)),
                SlideDirection.Bottom => new Translate(0, Length.Percent(100)),
                _ => new Translate(0, 0)
            };

            if (apply)
                target.style.translate = offset;
        }

        private static void RunShake(VisualElement target, ElementAnimationConfig config)
        {
            var totalMs = (long)(config.Duration * 1000);
            var stepMs = 50L;
            var amplitude = 5f;

            for (long elapsed = 0; elapsed < totalMs; elapsed += stepMs)
            {
                var t = elapsed;
                var decay = 1f - (float)t / totalMs;
                target.schedule.Execute(() =>
                {
                    var offsetX = UnityEngine.Random.Range(-amplitude, amplitude) * decay;
                    var offsetY = UnityEngine.Random.Range(-amplitude, amplitude) * decay;
                    target.style.translate = new Translate(offsetX, offsetY);
                }).StartingIn(t);
            }

            target.schedule.Execute(() =>
            {
                target.style.translate = new Translate(0, 0);
            }).StartingIn(totalMs);
        }

        private static EasingFunction ToEasingFunction(EasingMode mode)
        {
            return mode switch
            {
                EasingMode.Linear => new EasingFunction(UnityEngine.UIElements.EasingMode.Linear),
                EasingMode.EaseInCubic => new EasingFunction(UnityEngine.UIElements.EasingMode.EaseIn),
                EasingMode.EaseOutCubic => new EasingFunction(UnityEngine.UIElements.EasingMode.EaseOut),
                EasingMode.EaseInOutCubic => new EasingFunction(UnityEngine.UIElements.EasingMode.EaseInOut),
                _ => new EasingFunction(UnityEngine.UIElements.EasingMode.EaseOut)
            };
        }
    }
}
