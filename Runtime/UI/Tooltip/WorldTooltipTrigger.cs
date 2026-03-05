using System.Collections.Generic;
using Unidad.Core.Abstractions;
using UnityEngine;
using UnityEngine.UIElements;
#if UNIDAD_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Unidad.Core.UI.Tooltip
{
    /// <summary>
    /// Central raycasting driver that detects hover on registered objects and shows/hides
    /// world-space tooltips. Auto-spawned by TooltipService on first Attach() call.
    /// Billboards active tooltips every frame (same pattern as AutoRoguePG).
    /// </summary>
    internal sealed class WorldTooltipDriver : MonoBehaviour
    {
        private TooltipService _service;
        private ITimeProvider _timeProvider;
        private Camera _cam;
        private readonly Dictionary<Collider, AttachedEntry> _entries = new();
        private Collider _hoveredCollider;
        private WorldTooltipHandle _activeHandle;
        private AttachedEntry _activeEntry;
        private bool _isFirstFrame;

        // Collision avoidance
        private static readonly Collider[] OverlapBuffer = new Collider[16];
        private const float TooltipCollisionHalfDepth = 0.01f;

        // Billboard quad half-extents: worldSpaceSize (512,128) / pixelsPerUnit (100) / 2
        private const float PanelHalfWidth = 2.56f;
        private const float PanelHalfHeight = 0.64f;
        private const float Padding = 0.01f;

        internal void Initialize(TooltipService service, ITimeProvider timeProvider)
        {
            _service = service;
            _timeProvider = timeProvider;
        }

        internal void Register(GameObject target, string text, TooltipStyle style, Vector3 offset,
            WorldTooltipCollision collision = WorldTooltipCollision.None,
            WorldTooltipShowMode showMode = WorldTooltipShowMode.FadeIn)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null)
            {
                Debug.LogWarning($"[Tooltip] Cannot attach world tooltip to '{target.name}' — no Collider found.");
                return;
            }

            _entries[collider] = new AttachedEntry
            {
                Target = target,
                Collider = collider,
                Text = text,
                Style = style,
                Offset = offset,
                Collision = collision,
                ShowMode = showMode
            };
        }

        internal void UpdateEntry(GameObject target, string text, TooltipStyle style, Vector3 offset,
            WorldTooltipCollision collision = WorldTooltipCollision.None,
            WorldTooltipShowMode showMode = WorldTooltipShowMode.FadeIn)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null || !_entries.ContainsKey(collider)) return;

            _entries[collider] = new AttachedEntry
            {
                Target = target,
                Collider = collider,
                Text = text,
                Style = style,
                Offset = offset,
                Collision = collision,
                ShowMode = showMode
            };
        }

        internal void Unregister(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null) return;

            _entries.Remove(collider);

            // If we're hovering this one, hide it
            if (_hoveredCollider == collider)
            {
                HideCurrent();
            }
        }

        internal bool HasEntry(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            return collider != null && _entries.ContainsKey(collider);
        }

        private void Update()
        {
            if (_service == null || _entries.Count == 0) return;

            _cam ??= Camera.main;
            if (_cam == null) return;

            // Raycast from camera through mouse position — include triggers so the
            // tooltip's own BoxCollider (isTrigger) is detected
#if UNIDAD_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;
            var mousePos = mouse.position.ReadValue();
#else
            var mousePos = Input.mousePosition;
#endif
            var ray = _cam.ScreenPointToRay(mousePos);
            Collider hitCollider = null;
            bool hoveringTooltip = false;

            if (UnityEngine.Physics.Raycast(ray, out var hit, 100f, ~0, QueryTriggerInteraction.Collide))
            {
                if (_entries.ContainsKey(hit.collider))
                {
                    hitCollider = hit.collider;
                }
                else if (_activeHandle != null && _activeHandle.Go != null
                         && hit.collider.gameObject == _activeHandle.Go)
                {
                    // Mouse is over the active tooltip's collider — keep it alive
                    hoveringTooltip = true;
                }
            }

            // Hovering the tooltip panel itself — keep alive
            if (hoveringTooltip)
            {
                UpdateBillboard();
                return;
            }

            // Still hovering the same registered target — keep alive
            if (hitCollider != null && hitCollider == _hoveredCollider)
            {
                UpdateBillboard();
                return;
            }

            // Switching to a different target or left everything — hide previous
            HideCurrent();

            _hoveredCollider = hitCollider;

            // Show new
            if (_hoveredCollider != null)
            {
                _activeEntry = _entries[_hoveredCollider];
                _activeHandle = _service.ShowWorldInternal(_activeEntry);
                _isFirstFrame = true;

                // Billboard immediately (positions at final clamped/collision-resolved pos)
                UpdateBillboard();

                // Reveal AFTER positioning so tooltip never appears at wrong location
                if (_activeHandle != null)
                    _service.RevealWorldInternal(_activeHandle, _activeEntry.ShowMode);
            }
        }

        private void UpdateBillboard()
        {
            if (_activeHandle == null || _activeHandle.Go == null || _cam == null) return;
            if (_activeEntry.Target == null) return;

            var t = _activeHandle.Go.transform;
            var targetPos = _activeEntry.Target.transform.position;
            var candidateWorld = targetPos + _activeEntry.Offset;

            // Read actual content width from cached container for accurate clamping.
            // The panel is 512x128 px at 100 ppu = 5.12x1.28 world units.
            // The tooltip-container auto-sizes to its text content within that panel.
            var halfWidth = PanelHalfWidth;
            var halfHeight = PanelHalfHeight;
            var container = _activeHandle.CachedContainer;
            if (container != null)
            {
                var resolvedW = container.resolvedStyle.width;
                var resolvedH = container.resolvedStyle.height;
                if (!float.IsNaN(resolvedW) && resolvedW > 0)
                    halfWidth = resolvedW / 100f / 2f;
                if (!float.IsNaN(resolvedH) && resolvedH > 0)
                    halfHeight = resolvedH / 100f / 2f;
            }

            var vp = _cam.WorldToViewportPoint(candidateWorld);
            var distance = vp.z;
            var frustumHeight = 2f * distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var frustumWidth = frustumHeight * _cam.aspect;

            var marginX = halfWidth / frustumWidth + Padding;
            var marginY = halfHeight / frustumHeight + Padding;

            var clampedX = Mathf.Clamp(vp.x, marginX, 1f - marginX);
            var clampedY = Mathf.Clamp(vp.y, marginY, 1f - marginY);

            if (Mathf.Abs(clampedX - vp.x) > 0.001f || Mathf.Abs(clampedY - vp.y) > 0.001f)
            {
                candidateWorld += _cam.transform.right * ((clampedX - vp.x) * frustumWidth)
                                + _cam.transform.up * ((clampedY - vp.y) * frustumHeight);
            }

            // Collision avoidance
            if (_activeEntry.Collision != WorldTooltipCollision.None)
            {
                candidateWorld = ApplyCollisionNudge(candidateWorld, t.position, halfWidth, halfHeight);
            }

            t.position = candidateWorld;
            t.LookAt(t.position + _cam.transform.forward);
        }

        private const int MaxPullIterations = 10;
        private const float PullStep = 0.3f;
        private const float CollisionLerpSpeed = 8f;

        private Vector3 ApplyCollisionNudge(Vector3 candidateWorld, Vector3 currentPos,
            float halfWidth, float halfHeight)
        {
            var camTransform = _cam.transform;
            var pullDir = -camTransform.forward; // toward camera
            var halfExtents = new Vector3(halfWidth, halfHeight, TooltipCollisionHalfDepth);
            var orientation = Quaternion.LookRotation(camTransform.forward, camTransform.up);

            // Compute the target no-overlap position by pulling toward camera
            var targetPos = candidateWorld;
            for (var iter = 0; iter < MaxPullIterations; iter++)
            {
                if (!HasRelevantOverlap(targetPos, halfExtents, orientation))
                    break;
                targetPos += pullDir * PullStep;
            }

            // On first frame, snap directly to resolved position (no lerp from spawn point)
            if (_isFirstFrame)
            {
                _isFirstFrame = false;
                return targetPos;
            }

            // Smoothly lerp from current position toward the resolved position
            return Vector3.Lerp(currentPos, targetPos, CollisionLerpSpeed * _timeProvider.DeltaTime);
        }

        private bool HasRelevantOverlap(Vector3 position, Vector3 halfExtents, Quaternion orientation)
        {
            var count = UnityEngine.Physics.OverlapBoxNonAlloc(position, halfExtents, OverlapBuffer, orientation);

            for (int i = 0; i < count; i++)
            {
                var overlappedCollider = OverlapBuffer[i];

                // Skip tooltip's own GameObject
                if (_activeHandle != null && overlappedCollider.gameObject == _activeHandle.Go) continue;

                if (_activeEntry.Collision == WorldTooltipCollision.TargetOnly)
                {
                    if (overlappedCollider == _activeEntry.Collider) return true;
                }
                else // AllObjects
                {
                    return true;
                }
            }

            return false;
        }

        private void HideCurrent()
        {
            if (_activeHandle != null)
            {
                _service.HideWorldInternal(_activeHandle);
                _activeHandle = null;
                _activeEntry = default;
            }

            _hoveredCollider = null;
        }

        private void OnDestroy()
        {
            HideCurrent();
        }

        internal struct AttachedEntry
        {
            public GameObject Target;
            public Collider Collider;
            public string Text;
            public TooltipStyle Style;
            public Vector3 Offset;
            public WorldTooltipCollision Collision;
            public WorldTooltipShowMode ShowMode;
        }
    }
}
