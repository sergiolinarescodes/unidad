using Unidad.Core.EventBus;
using Unidad.Core.Systems;
using Unidad.Core.UI.Events;
using Unidad.Core.UI.TextAnimation;
using Unidad.Core.UI.WorldSpace;
using TextAnimationsForUIToolkit.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    internal sealed class FloatingTextService : SystemServiceBase, IFloatingTextService
    {
        private readonly ITextAnimationService _textAnimationService;
        private readonly IWorldUIService _worldUIService;
        private readonly PanelSettings _panelSettings;

        public FloatingTextService(
            IEventBus eventBus,
            ITextAnimationService textAnimationService,
            IWorldUIService worldUIService,
            PanelSettings panelSettings) : base(eventBus)
        {
            _textAnimationService = textAnimationService;
            _worldUIService = worldUIService;
            _panelSettings = panelSettings;
        }

        public void Spawn(Vector3 worldPosition, string text, FloatingTextStyle style = null)
        {
            style ??= FloatingTextStyle.Info;

            // Create a temporary transform at the position
            var tempGo = new GameObject("FloatingTextAnchor");
            tempGo.transform.position = worldPosition;

            SpawnInternal(tempGo.transform, text, style, autoDestroyAnchor: true);
        }

        public void Spawn(Transform follow, string text, FloatingTextStyle style = null)
        {
            style ??= FloatingTextStyle.Info;
            SpawnInternal(follow, text, style, autoDestroyAnchor: false);
        }

        private void SpawnInternal(Transform anchor, string text, FloatingTextStyle style, bool autoDestroyAnchor)
        {
            var settings = new WorldUISettings
            {
                Offset = Vector3.up * 1.5f,
                Billboard = true,
                Scale = 0.01f
            };

            var handle = _worldUIService.Attach(anchor, _panelSettings, settings);
            if (handle?.Root == null)
            {
                if (autoDestroyAnchor && anchor != null)
                    Object.Destroy(anchor.gameObject);
                return;
            }

            var label = _textAnimationService.CreateLabel("floating");
            label.AddToClassList("unidad-floating-text");

            if (!string.IsNullOrEmpty(style.UssClass))
                label.AddToClassList(style.UssClass);

            handle.Root.Add(label);

            var formattedText = _textAnimationService.ApplyRecipe(style.RecipeName, text);
            var cleaned = false;

            void Cleanup()
            {
                if (cleaned) return;
                cleaned = true;
                label.textVanishingFinished -= OnVanishFinished;
                _worldUIService.Detach(handle);
                if (autoDestroyAnchor && anchor != null && anchor.gameObject != null)
                    Object.Destroy(anchor.gameObject);
            }

            void OnVanishFinished(TextVanishingFinishedEvent _) => Cleanup();

            label.textVanishingFinished += OnVanishFinished;

            // Fallback auto-destroy timer
            if (handle.GameObject != null)
            {
                var duration = style.Duration;
                handle.Root.schedule.Execute(() => Cleanup())
                    .StartingIn((long)(duration * 1000) + 500);
            }

            _textAnimationService.PlayTypewriter(label, formattedText);
            Publish(new FloatingTextSpawnedEvent(anchor.position, text));
        }
    }
}
