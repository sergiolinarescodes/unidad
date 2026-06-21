using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;
using Unidad.Core.UI.Events;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using Unidad.Core.UI.WorldSpace;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unidad.Core.UI.Tooltip
{
    internal sealed class TooltipService : SystemServiceBase, ITooltipService
    {
        private readonly IElementAnimator _elementAnimator;
        private readonly ITimeProvider _timeProvider;
        private VisualElement _tooltipLayer;
        private Action<VisualElement> _frameDecorator;
        private int _nextId;
        private readonly Dictionary<int, TooltipHandle> _activeTooltips = new();
        private readonly Dictionary<int, WorldTooltipHandle> _activeWorldTooltips = new();
        private WorldTooltipDriver _worldDriver;

        // Screen-space Attach tracking
        private readonly Dictionary<VisualElement, IDisposable> _screenAttachments = new();

        // World-space pool
        private const int MaxWorldPoolSize = 10;
        private readonly Queue<WorldPoolEntry> _worldPool = new();
        private PanelSettings _worldPanelSettings;
        private VisualTreeAsset _worldTemplate;
        private GameObject _worldRoot;

        public TooltipService(
            IEventBus eventBus,
            IElementAnimator elementAnimator,
            ITimeProvider timeProvider = null) : base(eventBus)
        {
            _elementAnimator = elementAnimator;
            _timeProvider = timeProvider ?? new UnityTimeProvider();
        }

        public void SetTooltipLayer(VisualElement layer)
        {
            _tooltipLayer = layer;
        }

        public void SetFrameDecorator(Action<VisualElement> decorator)
        {
            _frameDecorator = decorator;
        }

        // Suppress the flat style border + radius and let the injected decorator paint the frame (no-op if none set).
        private void ApplyFrame(VisualElement container)
        {
            if (_frameDecorator == null) return;
            container.style.borderTopWidth = 0;
            container.style.borderBottomWidth = 0;
            container.style.borderLeftWidth = 0;
            container.style.borderRightWidth = 0;
            container.style.borderTopLeftRadius = 0;
            container.style.borderTopRightRadius = 0;
            container.style.borderBottomLeftRadius = 0;
            container.style.borderBottomRightRadius = 0;
            _frameDecorator(container);
        }

        // ── Screen-space tooltips ──

        public TooltipHandle Show(TooltipContent content, TooltipAnchor anchor,
            TooltipPlacement placement = TooltipPlacement.Auto, TooltipStyle style = null)
        {
            if (_tooltipLayer == null)
            {
                Debug.LogWarning("[Tooltip] No tooltip layer set. Call SetTooltipLayer() first.");
                return null;
            }

            style ??= TooltipStyle.Default;
            var id = _nextId++;

            // Build tooltip visual tree
            var container = new VisualElement { name = $"tooltip-{id}" };
            container.AddToClassList("unidad-tooltip");
            container.style.position = Position.Absolute;
            container.style.backgroundColor = style.BackgroundColor;
            container.style.borderTopColor = style.BorderColor;
            container.style.borderBottomColor = style.BorderColor;
            container.style.borderLeftColor = style.BorderColor;
            container.style.borderRightColor = style.BorderColor;
            container.style.borderTopWidth = style.BorderWidth;
            container.style.borderBottomWidth = style.BorderWidth;
            container.style.borderLeftWidth = style.BorderWidth;
            container.style.borderRightWidth = style.BorderWidth;
            container.style.borderTopLeftRadius = style.BorderRadius;
            container.style.borderTopRightRadius = style.BorderRadius;
            container.style.borderBottomLeftRadius = style.BorderRadius;
            container.style.borderBottomRightRadius = style.BorderRadius;
            container.style.paddingLeft = style.PaddingH;
            container.style.paddingRight = style.PaddingH;
            container.style.paddingTop = style.PaddingV;
            container.style.paddingBottom = style.PaddingV;
            container.style.maxWidth = style.MaxWidth;
            container.style.overflow = Overflow.Hidden;
            container.style.opacity = 0;
            container.pickingMode = PickingMode.Ignore;

            // Content
            if (content.IsCustom)
            {
                var custom = content.CustomBuilder();
                container.Add(custom);
            }
            else
            {
                var label = new Label(content.Text);
                label.AddToClassList("unidad-tooltip__text");
                label.style.color = style.TextColor;
                label.style.fontSize = style.FontSize;
                label.style.whiteSpace = WhiteSpace.Normal;
                container.Add(label);
            }

            // Arrow
            VisualElement arrow = null;
            if (style.ShowArrow)
            {
                arrow = new VisualElement { name = "tooltip-arrow" };
                arrow.AddToClassList("unidad-tooltip__arrow");
                arrow.style.position = Position.Absolute;
                arrow.style.width = 0;
                arrow.style.height = 0;
                arrow.pickingMode = PickingMode.Ignore;
                container.Add(arrow);
            }

            ApplyFrame(container); // injected fine-frame overlay (drawn on top of the content)

            var handle = new TooltipHandle(id, container, arrow);
            _activeTooltips[id] = handle;
            _tooltipLayer.Add(container);

            // Wait for layout to measure size, then position
            var anchorRef = anchor;
            var styleRef = style;
            var placementRef = placement;
            container.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            void OnGeometryChanged(GeometryChangedEvent evt)
            {
                container.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                PositionTooltip(handle, anchorRef, placementRef, styleRef);

                // Fade in
                _elementAnimator.Animate(container,
                    new ElementAnimationConfig(ElementAnimationType.FadeIn, styleRef.FadeInDuration));
            }

            Publish(new TooltipShownEvent(id, false));

            // Schedule sub-tooltips if any
            if (content.SubTooltips != null && content.SubTooltips.Count > 0)
            {
                var contentRef = content;
                handle.SubTooltipTimer = container.schedule.Execute(() =>
                {
                    ShowSubTooltips(handle, contentRef, style ?? TooltipStyle.Default);
                }).StartingIn((long)(style ?? TooltipStyle.Default).SubTooltipDelayMs);
            }

            return handle;
        }

        public void Hide(TooltipHandle handle)
        {
            if (handle == null || !_activeTooltips.ContainsKey(handle.Id))
                return;

            // Cancel pending sub-tooltip timer
            handle.SubTooltipTimer?.Pause();
            handle.SubTooltipTimer = null;

            // Cascade hide sub-tooltips
            foreach (var sub in handle.SubHandles)
            {
                if (_activeTooltips.ContainsKey(sub.Id))
                {
                    _activeTooltips.Remove(sub.Id);
                    _elementAnimator.Animate(sub.Root,
                        new ElementAnimationConfig(ElementAnimationType.FadeOut, 0.1f),
                        () => sub.Root.RemoveFromHierarchy());
                }
            }
            handle.SubHandles.Clear();

            _activeTooltips.Remove(handle.Id);

            _elementAnimator.Animate(handle.Root,
                new ElementAnimationConfig(ElementAnimationType.FadeOut, 0.1f),
                () => handle.Root.RemoveFromHierarchy());

            Publish(new TooltipHiddenEvent(handle.Id, false));
        }

        public void HideAll()
        {
            foreach (var kvp in new Dictionary<int, TooltipHandle>(_activeTooltips))
                Hide(kvp.Value);

            foreach (var kvp in new Dictionary<int, WorldTooltipHandle>(_activeWorldTooltips))
                HideWorldInternal(kvp.Value);
        }

        private void ShowSubTooltips(TooltipHandle parentHandle, TooltipContent parentContent, TooltipStyle parentStyle)
        {
            if (!_activeTooltips.ContainsKey(parentHandle.Id)) return;
            if (_tooltipLayer == null) return;

            var parentRect = parentHandle.Root.worldBound;
            var gap = parentStyle.SubTooltipGap;
            float yOffset = 0f;

            foreach (var entry in parentContent.SubTooltips)
            {
                var subStyle = entry.Style ?? TooltipStyle.Default;
                var subId = _nextId++;

                var container = new VisualElement { name = $"sub-tooltip-{subId}" };
                container.AddToClassList("unidad-tooltip");
                container.AddToClassList("unidad-sub-tooltip");
                container.style.position = Position.Absolute;
                container.style.backgroundColor = subStyle.BackgroundColor;
                container.style.borderTopColor = subStyle.BorderColor;
                container.style.borderBottomColor = subStyle.BorderColor;
                container.style.borderLeftColor = subStyle.BorderColor;
                container.style.borderRightColor = subStyle.BorderColor;
                container.style.borderTopWidth = subStyle.BorderWidth;
                container.style.borderBottomWidth = subStyle.BorderWidth;
                container.style.borderLeftWidth = subStyle.BorderWidth;
                container.style.borderRightWidth = subStyle.BorderWidth;
                container.style.borderTopLeftRadius = subStyle.BorderRadius;
                container.style.borderTopRightRadius = subStyle.BorderRadius;
                container.style.borderBottomLeftRadius = subStyle.BorderRadius;
                container.style.borderBottomRightRadius = subStyle.BorderRadius;
                container.style.paddingLeft = subStyle.PaddingH;
                container.style.paddingRight = subStyle.PaddingH;
                container.style.paddingTop = subStyle.PaddingV;
                container.style.paddingBottom = subStyle.PaddingV;
                container.style.maxWidth = subStyle.MaxWidth;
                container.style.opacity = 0;
                container.pickingMode = PickingMode.Ignore;

                if (entry.Content.IsCustom)
                {
                    var custom = entry.Content.CustomBuilder();
                    container.Add(custom);
                }
                else
                {
                    var label = new Label(entry.Content.Text);
                    label.AddToClassList("unidad-tooltip__text");
                    label.style.color = subStyle.TextColor;
                    label.style.fontSize = subStyle.FontSize;
                    label.style.whiteSpace = WhiteSpace.Normal;
                    container.Add(label);
                }

                ApplyFrame(container); // sub-tooltips wear the same injected fine-frame

                var subHandle = new TooltipHandle(subId, container, null);
                _activeTooltips[subId] = subHandle;
                parentHandle.SubHandles.Add(subHandle);
                _tooltipLayer.Add(container);

                // Position after layout — with clamping/flipping
                var preferredPlacement = entry.PreferredPlacement;
                var capturedOffset = yOffset;
                container.RegisterCallback<GeometryChangedEvent>(OnSubLayout);

                void OnSubLayout(GeometryChangedEvent evt)
                {
                    container.UnregisterCallback<GeometryChangedEvent>(OnSubLayout);

                    var subSize = new Vector2(container.resolvedStyle.width, container.resolvedStyle.height);
                    var containerSize = new Vector2(
                        _tooltipLayer.resolvedStyle.width,
                        _tooltipLayer.resolvedStyle.height);

                    // Try preferred placement, flip if it doesn't fit
                    var resolvedPlacement = preferredPlacement;
                    var pos = ComputeSubPosition(resolvedPlacement, parentRect, subSize, gap, capturedOffset);

                    if (!FitsInBounds(pos, subSize, containerSize))
                    {
                        // Flip horizontally or vertically
                        resolvedPlacement = FlipPlacement(resolvedPlacement);
                        pos = ComputeSubPosition(resolvedPlacement, parentRect, subSize, gap, capturedOffset);

                        // If still doesn't fit, try the other axis
                        if (!FitsInBounds(pos, subSize, containerSize))
                        {
                            resolvedPlacement = resolvedPlacement is TooltipPlacement.Top or TooltipPlacement.Bottom
                                ? TooltipPlacement.Right : TooltipPlacement.Bottom;
                            pos = ComputeSubPosition(resolvedPlacement, parentRect, subSize, gap, capturedOffset);

                            if (!FitsInBounds(pos, subSize, containerSize))
                            {
                                resolvedPlacement = FlipPlacement(resolvedPlacement);
                                pos = ComputeSubPosition(resolvedPlacement, parentRect, subSize, gap, capturedOffset);
                            }
                        }
                    }

                    // Final clamp to container bounds
                    pos.x = Mathf.Clamp(pos.x, 0, Mathf.Max(0, containerSize.x - subSize.x));
                    pos.y = Mathf.Clamp(pos.y, 0, Mathf.Max(0, containerSize.y - subSize.y));

                    container.style.left = Mathf.Round(pos.x);
                    container.style.top = Mathf.Round(pos.y);

                    var slideDir = resolvedPlacement switch
                    {
                        TooltipPlacement.Right => SlideDirection.Left,
                        TooltipPlacement.Left => SlideDirection.Right,
                        TooltipPlacement.Bottom => SlideDirection.Top,
                        TooltipPlacement.Top => SlideDirection.Bottom,
                        _ => SlideDirection.Left
                    };

                    _elementAnimator.Animate(container,
                        new ElementAnimationConfig(ElementAnimationType.SlideIn, 0.2f, slideDir));
                    _elementAnimator.Animate(container,
                        new ElementAnimationConfig(ElementAnimationType.FadeIn, 0.2f));
                }

                yOffset += 50f; // approximate height; stacks vertically along parent edge
            }
        }

        private static Vector2 ComputeSubPosition(TooltipPlacement placement, Rect parentRect,
            Vector2 subSize, float gap, float stackOffset)
        {
            return placement switch
            {
                TooltipPlacement.Right => new Vector2(parentRect.xMax + gap, parentRect.yMin + stackOffset),
                TooltipPlacement.Left => new Vector2(parentRect.xMin - subSize.x - gap, parentRect.yMin + stackOffset),
                TooltipPlacement.Bottom => new Vector2(parentRect.xMin + stackOffset, parentRect.yMax + gap),
                TooltipPlacement.Top => new Vector2(parentRect.xMin + stackOffset, parentRect.yMin - subSize.y - gap),
                _ => new Vector2(parentRect.xMax + gap, parentRect.yMin + stackOffset)
            };
        }

        private static bool FitsInBounds(Vector2 pos, Vector2 size, Vector2 container)
        {
            return pos.x >= 0 && pos.y >= 0 &&
                   pos.x + size.x <= container.x &&
                   pos.y + size.y <= container.y;
        }

        private static TooltipPlacement FlipPlacement(TooltipPlacement p) => p switch
        {
            TooltipPlacement.Right => TooltipPlacement.Left,
            TooltipPlacement.Left => TooltipPlacement.Right,
            TooltipPlacement.Top => TooltipPlacement.Bottom,
            TooltipPlacement.Bottom => TooltipPlacement.Top,
            _ => p
        };

        private void PositionTooltip(TooltipHandle handle, TooltipAnchor anchor,
            TooltipPlacement placement, TooltipStyle style)
        {
            var container = handle.Root;
            var anchorRect = anchor.ResolveScreenRect(_tooltipLayer);
            var tooltipSize = new Vector2(container.resolvedStyle.width, container.resolvedStyle.height);
            var containerSize = new Vector2(_tooltipLayer.resolvedStyle.width, _tooltipLayer.resolvedStyle.height);

            var result = TooltipPositioner.Compute(anchorRect, tooltipSize, containerSize, placement, style.ArrowSize);
            handle.ResolvedPlacement = result.Placement;

            // Round to whole pixels so the tooltip's border/frame never straddles a device-pixel boundary
            // (the "fat/uneven border depending on where the tooltip lands" artifact).
            container.style.left = Mathf.Round(result.Position.x);
            container.style.top = Mathf.Round(result.Position.y);

            if (style.ShowArrow && handle.Arrow != null)
                StyleArrow(handle.Arrow, result.Placement, result.ArrowOffset, style);
        }

        private static void StyleArrow(VisualElement arrow, TooltipPlacement placement,
            Vector2 arrowOffset, TooltipStyle style)
        {
            var size = style.ArrowSize;
            var transparent = Color.clear;
            var color = style.BackgroundColor;

            // Reset all borders
            arrow.style.borderTopWidth = 0;
            arrow.style.borderBottomWidth = 0;
            arrow.style.borderLeftWidth = 0;
            arrow.style.borderRightWidth = 0;
            arrow.style.borderTopColor = transparent;
            arrow.style.borderBottomColor = transparent;
            arrow.style.borderLeftColor = transparent;
            arrow.style.borderRightColor = transparent;

            // CSS triangle technique
            switch (placement)
            {
                case TooltipPlacement.Bottom:
                    // Arrow points up (on top edge of tooltip)
                    arrow.style.borderLeftWidth = size;
                    arrow.style.borderRightWidth = size;
                    arrow.style.borderBottomWidth = size;
                    arrow.style.borderLeftColor = transparent;
                    arrow.style.borderRightColor = transparent;
                    arrow.style.borderBottomColor = color;
                    arrow.style.top = -size;
                    arrow.style.bottom = StyleKeyword.Auto;
                    arrow.style.left = Length.Percent(50);
                    arrow.style.marginLeft = -size + arrowOffset.x;
                    break;

                case TooltipPlacement.Top:
                    // Arrow points down (on bottom edge of tooltip)
                    arrow.style.borderLeftWidth = size;
                    arrow.style.borderRightWidth = size;
                    arrow.style.borderTopWidth = size;
                    arrow.style.borderLeftColor = transparent;
                    arrow.style.borderRightColor = transparent;
                    arrow.style.borderTopColor = color;
                    arrow.style.bottom = -size;
                    arrow.style.top = StyleKeyword.Auto;
                    arrow.style.left = Length.Percent(50);
                    arrow.style.marginLeft = -size + arrowOffset.x;
                    break;

                case TooltipPlacement.Right:
                    // Arrow points left (on left edge of tooltip)
                    arrow.style.borderTopWidth = size;
                    arrow.style.borderBottomWidth = size;
                    arrow.style.borderRightWidth = size;
                    arrow.style.borderTopColor = transparent;
                    arrow.style.borderBottomColor = transparent;
                    arrow.style.borderRightColor = color;
                    arrow.style.left = -size;
                    arrow.style.right = StyleKeyword.Auto;
                    arrow.style.top = Length.Percent(50);
                    arrow.style.marginTop = -size + arrowOffset.y;
                    break;

                case TooltipPlacement.Left:
                    // Arrow points right (on right edge of tooltip)
                    arrow.style.borderTopWidth = size;
                    arrow.style.borderBottomWidth = size;
                    arrow.style.borderLeftWidth = size;
                    arrow.style.borderTopColor = transparent;
                    arrow.style.borderBottomColor = transparent;
                    arrow.style.borderLeftColor = color;
                    arrow.style.right = -size;
                    arrow.style.left = StyleKeyword.Auto;
                    arrow.style.top = Length.Percent(50);
                    arrow.style.marginTop = -size + arrowOffset.y;
                    break;
            }
        }

        // ── Hover integration ──

        public IDisposable RegisterHover(VisualElement target, TooltipContent content,
            TooltipPlacement placement = TooltipPlacement.Auto,
            TooltipStyle style = null, float delayMs = 400f)
        {
            TooltipHandle currentHandle = null;
            IVisualElementScheduledItem pendingShow = null;

            void OnPointerEnter(PointerEnterEvent evt)
            {
                pendingShow = target.schedule.Execute(() =>
                {
                    var anchor = TooltipAnchor.FromElement(target);
                    currentHandle = Show(content, anchor, placement, style);
                }).StartingIn((long)delayMs);
            }

            void OnPointerLeave(PointerLeaveEvent evt)
            {
                pendingShow?.Pause();
                pendingShow = null;

                if (currentHandle != null)
                {
                    Hide(currentHandle);
                    currentHandle = null;
                }
            }

            target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);

            return new ActionDisposable(() =>
            {
                target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
                target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);

                pendingShow?.Pause();
                if (currentHandle != null)
                    Hide(currentHandle);
            });
        }

        // ── Screen-space: Attach / Detach API ──

        public void Attach(VisualElement target, string text, TooltipStyle style = null,
            TooltipPlacement placement = TooltipPlacement.Auto, float delayMs = 400f)
        {
            if (target == null) return;

            // If already attached, detach first
            if (_screenAttachments.TryGetValue(target, out var existing))
                existing.Dispose();

            var subscription = RegisterHover(target, TooltipContent.FromText(text), placement, style, delayMs);
            _screenAttachments[target] = subscription;
        }

        public void Detach(VisualElement target)
        {
            if (target == null || !_screenAttachments.TryGetValue(target, out var subscription))
                return;

            subscription.Dispose();
            _screenAttachments.Remove(target);
        }

        // ── World-space: Attach / Detach API ──

        public void Attach(GameObject target, string text, TooltipStyle style = null,
            Vector3 offset = default, WorldTooltipCollision collision = WorldTooltipCollision.None,
            WorldTooltipShowMode showMode = WorldTooltipShowMode.FadeIn)
        {
            if (target == null) return;

            var resolvedStyle = style ?? TooltipStyle.Default;
            var resolvedOffset = offset == default ? Vector3.up * 0.8f : offset;

            EnsureWorldDriver();

            if (_worldDriver.HasEntry(target))
            {
                _worldDriver.UpdateEntry(target, text, resolvedStyle, resolvedOffset, collision, showMode);
                return;
            }

            _worldDriver.Register(target, text, resolvedStyle, resolvedOffset, collision, showMode);
        }

        public void Detach(GameObject target)
        {
            if (target == null || _worldDriver == null) return;
            _worldDriver.Unregister(target);
        }

        private void EnsureWorldDriver()
        {
            if (_worldDriver != null) return;

            EnsureWorldResources();
            var driverGo = new GameObject("[WorldTooltipDriver]");
            if (_worldRoot != null)
                driverGo.transform.SetParent(_worldRoot.transform);
            _worldDriver = driverGo.AddComponent<WorldTooltipDriver>();
            _worldDriver.Initialize(this, _timeProvider);
        }

        // ── World-space: Internal show/hide (called by WorldTooltipDriver) ──

        internal WorldTooltipHandle ShowWorldInternal(WorldTooltipDriver.AttachedEntry entry)
        {
            var style = entry.Style ?? TooltipStyle.Default;

            EnsureWorldResources();
            if (_worldPanelSettings == null) return null;

            var id = _nextId++;

            // Acquire from pool or create
            WorldPoolEntry poolEntry;
            if (_worldPool.Count > 0)
            {
                poolEntry = _worldPool.Dequeue();
                poolEntry.Go.SetActive(true);
            }
            else
            {
                poolEntry = CreateWorldEntry();
            }

            // Position and billboard
            var t = poolEntry.Go.transform;
            if (entry.Target != null)
                t.position = entry.Target.transform.position + entry.Offset;
            t.localScale = Vector3.one;

            var cam = Camera.main;
            if (cam != null)
                t.LookAt(t.position + cam.transform.forward);

            // Configure tooltip — only set text and colors; UXML template owns sizing
            var root = poolEntry.Document.rootVisualElement;
            var container = root.Q<VisualElement>("tooltip-container");
            var label = root.Q<Label>("tooltip-text");

            if (container != null)
            {
                container.style.backgroundColor = style.BackgroundColor;
                container.style.borderTopColor = style.BorderColor;
                container.style.borderBottomColor = style.BorderColor;
                container.style.borderLeftColor = style.BorderColor;
                container.style.borderRightColor = style.BorderColor;
            }

            if (label != null)
            {
                label.text = entry.Text ?? "";
                label.style.color = style.TextColor;
            }

            // Leave hidden — WorldTooltipDriver will reveal after positioning
            root.style.display = DisplayStyle.None;

            var handle = new WorldTooltipHandle(id, poolEntry.Go, poolEntry.Document)
            {
                CachedContainer = container
            };
            _activeWorldTooltips[id] = handle;

            Publish(new TooltipShownEvent(id, true));
            return handle;
        }

        internal void RevealWorldInternal(WorldTooltipHandle handle, WorldTooltipShowMode showMode)
        {
            if (handle == null) return;

            var root = handle.Document?.rootVisualElement;
            if (root == null) return;

            if (showMode == WorldTooltipShowMode.Instant)
            {
                root.style.opacity = 1f;
                root.style.display = DisplayStyle.Flex;
            }
            else
            {
                // CSS transitions don't fire on elements going from display:none → flex
                // in the same frame. Set opacity=0, make visible, then schedule the
                // fade-in on the next frame so the element is in layout first.
                root.style.opacity = 0f;
                root.style.display = DisplayStyle.Flex;
                root.schedule.Execute(() =>
                {
                    _elementAnimator.Animate(root,
                        new ElementAnimationConfig(ElementAnimationType.FadeIn, 0.15f));
                });
            }
        }

        internal void HideWorldInternal(WorldTooltipHandle handle)
        {
            if (handle == null || !_activeWorldTooltips.ContainsKey(handle.Id))
                return;

            _activeWorldTooltips.Remove(handle.Id);

            // Return to pool
            if (handle.Go != null)
            {
                var root = handle.Document.rootVisualElement;
                if (root != null)
                    root.style.display = DisplayStyle.None;

                handle.Go.SetActive(false);
                handle.Go.transform.SetParent(_worldRoot != null ? _worldRoot.transform : null);

                if (_worldPool.Count < MaxWorldPoolSize)
                {
                    _worldPool.Enqueue(new WorldPoolEntry { Go = handle.Go, Document = handle.Document });
                }
                else
                {
                    Object.Destroy(handle.Go);
                }
            }

            Publish(new TooltipHiddenEvent(handle.Id, true));
        }

        private void EnsureWorldResources()
        {
            if (_worldPanelSettings == null)
            {
                _worldPanelSettings = Resources.Load<PanelSettings>("UI/WorldSpacePanelSettings");
                if (_worldPanelSettings == null)
                {
                    Debug.LogWarning("[Tooltip] WorldSpacePanelSettings not found in Resources/UI/. World tooltips will not render.");
                    return;
                }
            }

            if (_worldTemplate == null)
            {
                _worldTemplate = Resources.Load<VisualTreeAsset>("UI/WorldTooltip");
                if (_worldTemplate == null)
                    Debug.LogWarning("[Tooltip] WorldTooltip.uxml not found in Resources/UI/. World tooltips will have no source asset.");
            }

            if (_worldRoot == null)
            {
                _worldRoot = new GameObject("[WorldTooltips]");
            }
        }

        private WorldPoolEntry CreateWorldEntry()
        {
            var uiDoc = WorldSpaceUIFactory.Create(
                $"WorldTooltip_{_nextId}",
                _worldRoot.transform,
                _worldPanelSettings,
                _worldTemplate,
                worldSpaceSize: new Vector2(512, 128),
                disablePicking: true);

            var root = uiDoc.rootVisualElement;

            // Root fills the panel; template children keep their own sizing
            // (tooltip-container is position:absolute with auto-sizing to fit text)
            root.style.width = new Length(100, LengthUnit.Percent);
            root.style.height = new Length(100, LengthUnit.Percent);

            WorldSpaceUIFactory.SetPickingModeRecursive(root, PickingMode.Ignore);
            root.style.display = DisplayStyle.None;

            // Add a thin BoxCollider matching the panel size so the raycast-based
            // hover check in WorldTooltipDriver can detect the mouse over the tooltip.
            var box = uiDoc.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(5.12f, 1.28f, 0.02f);
            box.isTrigger = true;

            return new WorldPoolEntry { Go = uiDoc.gameObject, Document = uiDoc };
        }

        public override void Dispose()
        {
            // Dispose screen-space attachments
            foreach (var kvp in _screenAttachments)
                kvp.Value.Dispose();
            _screenAttachments.Clear();

            // Hide all tooltips before destroying the driver
            HideAll();

            if (_worldDriver != null)
            {
                Object.Destroy(_worldDriver.gameObject);
                _worldDriver = null;
            }

            while (_worldPool.Count > 0)
            {
                var entry = _worldPool.Dequeue();
                if (entry.Go != null)
                    Object.Destroy(entry.Go);
            }

            if (_worldRoot != null)
            {
                Object.Destroy(_worldRoot);
                _worldRoot = null;
            }

            _worldPanelSettings = null;
            _worldTemplate = null;

            base.Dispose();
        }

        private class WorldPoolEntry
        {
            public GameObject Go;
            public UIDocument Document;
        }
    }
}
