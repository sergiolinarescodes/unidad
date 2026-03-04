using System;
using System.Collections.Generic;
using PrimeTween;
using Unidad.Core.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unidad.Core.UI.WorldSpace
{
    internal sealed class WorldFloatingTextService : IWorldFloatingTextService
    {
        private const int DefaultPrewarmCount = 10;
        private const int MaxPoolSize = 20;
        private const float DefaultAnimationDuration = 0.8f;
        private const float PunchScaleAmount = 1.4f;
        private const float PunchScaleDuration = 0.1f;

        private static readonly Vector2 FloatingTextWorldSpaceSize = new(256, 128);

        private GameObject _root;
        private PanelSettings _panelSettings;
        private VisualTreeAsset _template;
        private readonly Queue<PooledEntry> _pool = new();
        private readonly HashSet<PooledEntry> _active = new();
        private bool _initialized;
        private int _instanceCount;

        public void Initialize(PanelSettings panelSettings, VisualTreeAsset template = null, int prewarmCount = DefaultPrewarmCount)
        {
            if (_initialized)
                Dispose();

            if (panelSettings == null)
                Debug.LogWarning("[WorldFloatingText] PanelSettings is null — world-space rendering will not work.");
            if (template == null)
                Debug.LogWarning("[WorldFloatingText] VisualTreeAsset template is null — UIDocuments will have no Source Asset.");

            _panelSettings = panelSettings;
            _template = template;
            _root = new GameObject("[WorldFloatingText]");
            _initialized = true;
            _instanceCount = 0;

            for (var i = 0; i < prewarmCount; i++)
            {
                var entry = CreateEntry();
                _pool.Enqueue(entry);
            }
        }

        public void Dispose()
        {
            if (!_initialized) return;

            // Clean active entries
            foreach (var entry in _active)
            {
                if (entry.Go != null)
                {
                    Tween.StopAll(entry.Go.transform);
                    Object.Destroy(entry.Go);
                }
            }
            _active.Clear();

            // Clean pooled entries
            while (_pool.Count > 0)
            {
                var entry = _pool.Dequeue();
                if (entry.Go != null)
                {
                    Tween.StopAll(entry.Go.transform);
                    Object.Destroy(entry.Go);
                }
            }

            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            _panelSettings = null;
            _template = null;
            _initialized = false;
            _instanceCount = 0;
        }

        public void Spawn(
            Vector3 worldPosition,
            string text,
            FloatingTextStyle style = null,
            FloatingTextAnimator animator = null)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[WorldFloatingText] Not initialized. Call Initialize() first.");
                return;
            }

            style ??= FloatingTextStyle.Info;

            var entry = Acquire();
            var transform = entry.Go.transform;

            // Configure label
            var root = entry.Document.rootVisualElement;
            root.style.display = DisplayStyle.Flex;

            entry.Label.text = text;
            entry.Label.style.color = style.Color;
            entry.Label.style.fontSize = style.FontSize;
            entry.Label.style.opacity = 1f;

            // Position
            transform.position = worldPosition;
            transform.localScale = Vector3.one;

            // Billboard — cache camera on entry
            entry.Cam = Camera.main;
            if (entry.Cam != null)
                transform.LookAt(transform.position + entry.Cam.transform.forward);

            var handle = new FloatingTextHandle(entry, this);

            if (animator != null)
            {
                try
                {
                    animator(handle);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WorldFloatingText] Custom animator threw: {ex}");
                    Return(entry);
                }
            }
            else
            {
                DefaultAnimate(handle, style);
            }
        }

        // ── Default animation ──

        private sealed class AnimationContext
        {
            public PooledEntry Entry;
            public Vector3 StartPos;
            public Vector3 EndPos;
            public bool FadeOut;
        }

        private void DefaultAnimate(FloatingTextHandle handle, FloatingTextStyle style)
        {
            var entry = handle.Entry;
            var transform = entry.Go.transform;

            var startPos = transform.position;
            var endPos = startPos + style.DriftOffset;

            var ctx = new AnimationContext
            {
                Entry = entry,
                StartPos = startPos,
                EndPos = endPos,
                FadeOut = style.FadeOut
            };

            // Scale punch
            Tween.Scale(
                transform,
                transform.localScale * PunchScaleAmount,
                PunchScaleDuration,
                Ease.OutBack,
                cycles: 2,
                cycleMode: CycleMode.Yoyo);

            // Drift + fade + billboard
            Tween.Custom(
                ctx,
                0f, 1f,
                style.Duration > 0 ? style.Duration : DefaultAnimationDuration,
                onValueChange: static (c, progress) =>
                {
                    if (c.Entry.Go == null) return;

                    var t = c.Entry.Go.transform;
                    t.position = Vector3.Lerp(c.StartPos, c.EndPos, progress);

                    if (c.FadeOut && progress > 0.5f)
                    {
                        var fade = (progress - 0.5f) * 2f;
                        c.Entry.Label.style.opacity = 1f - fade;
                    }

                    var cam = c.Entry.Cam;
                    if (cam != null)
                        t.LookAt(t.position + cam.transform.forward);
                },
                Ease.OutQuad
            ).OnComplete(entry, static e => e.Owner.Return(e));
        }

        // ── Pool management ──

        internal void ReturnToPool(FloatingTextHandle handle)
        {
            if (handle.Entry != null)
                Return(handle.Entry);
        }

        private PooledEntry Acquire()
        {
            PooledEntry entry;
            if (_pool.Count > 0)
            {
                entry = _pool.Dequeue();
            }
            else
            {
                Debug.LogWarning($"[WorldFloatingText] Pool exhausted ({_instanceCount} instances). Consider increasing prewarmCount.");
                entry = CreateEntry();
            }

            _active.Add(entry);
            return entry;
        }

        internal void Return(PooledEntry entry)
        {
            if (entry.Go == null) return;

            _active.Remove(entry);

            Tween.StopAll(entry.Go.transform);

            var root = entry.Document.rootVisualElement;
            root.style.display = DisplayStyle.None;

            entry.Cam = null;

            // Enforce max pool size — destroy excess entries instead of re-pooling
            if (_pool.Count >= MaxPoolSize)
            {
                Object.Destroy(entry.Go);
                return;
            }

            entry.Go.transform.SetParent(_root != null ? _root.transform : null);
            entry.Go.transform.localPosition = Vector3.zero;
            entry.Go.transform.localScale = Vector3.one;

            _pool.Enqueue(entry);
        }

        private PooledEntry CreateEntry()
        {
            var uiDoc = WorldSpaceUIFactory.Create(
                $"FloatingText_{_instanceCount++}",
                _root.transform,
                _panelSettings,
                _template,
                worldSpaceSize: FloatingTextWorldSpaceSize,
                disablePicking: false);

            var root = uiDoc.rootVisualElement;

            // Ensure root and any TemplateContainer fill the entire panel (worldSpaceSize).
            root.style.width = new Length(100, LengthUnit.Percent);
            root.style.height = new Length(100, LengthUnit.Percent);
            foreach (var child in root.Children())
            {
                child.style.width = new Length(100, LengthUnit.Percent);
                child.style.height = new Length(100, LengthUnit.Percent);
            }

            // Query from template or create fallback
            var label = root.Q<Label>("floating-text");
            if (label == null)
            {
                root.Clear();
                label = new Label
                {
                    name = "floating-text",
                    style =
                    {
                        position = Position.Absolute,
                        left = 0, top = 0, right = 0, bottom = 0,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        backgroundColor = Color.clear
                    }
                };
                root.Add(label);
            }

            WorldSpaceUIFactory.SetPickingModeRecursive(root, PickingMode.Ignore);
            root.style.display = DisplayStyle.None;

            return new PooledEntry
            {
                Go = uiDoc.gameObject,
                Document = uiDoc,
                Label = label,
                Owner = this
            };
        }

        internal class PooledEntry
        {
            public GameObject Go;
            public UIDocument Document;
            public Label Label;
            public Camera Cam;
            public WorldFloatingTextService Owner;
        }
    }
}
