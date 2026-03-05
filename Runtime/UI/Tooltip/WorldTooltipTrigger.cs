using System.Collections.Generic;
using UnityEngine;
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
        private Camera _cam;
        private readonly Dictionary<Collider, AttachedEntry> _entries = new();
        private Collider _hoveredCollider;
        private WorldTooltipHandle _activeHandle;
        private AttachedEntry _activeEntry;

        internal void Initialize(TooltipService service)
        {
            _service = service;
        }

        internal void Register(GameObject target, string text, TooltipStyle style, Vector3 offset)
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
                Offset = offset
            };
        }

        internal void UpdateEntry(GameObject target, string text, TooltipStyle style, Vector3 offset)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null || !_entries.ContainsKey(collider)) return;

            _entries[collider] = new AttachedEntry
            {
                Target = target,
                Collider = collider,
                Text = text,
                Style = style,
                Offset = offset
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

            // Raycast from camera through mouse position
#if UNIDAD_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;
            var mousePos = mouse.position.ReadValue();
#else
            var mousePos = Input.mousePosition;
#endif
            var ray = _cam.ScreenPointToRay(mousePos);
            Collider hitCollider = null;

            if (UnityEngine.Physics.Raycast(ray, out var hit, 100f))
            {
                if (_entries.ContainsKey(hit.collider))
                    hitCollider = hit.collider;
            }

            // No change
            if (hitCollider == _hoveredCollider)
            {
                // Billboard active tooltip every frame
                UpdateBillboard();
                return;
            }

            // Hide previous
            HideCurrent();

            _hoveredCollider = hitCollider;

            // Show new
            if (_hoveredCollider != null)
            {
                _activeEntry = _entries[_hoveredCollider];
                _activeHandle = _service.ShowWorldInternal(_activeEntry);

                // Billboard immediately
                UpdateBillboard();
            }
        }

        // World-space half-size of tooltip panel: worldSpaceSize (512,128) / pixelsPerUnit (100) / 2
        private const float TooltipWorldHalfWidth = 2.56f;
        private const float TooltipWorldHalfHeight = 0.64f;
        private const float Padding = 0.01f;

        private void UpdateBillboard()
        {
            if (_activeHandle == null || _activeHandle.Go == null || _cam == null) return;
            if (_activeEntry.Target == null) return;

            var t = _activeHandle.Go.transform;
            var targetPos = _activeEntry.Target.transform.position;
            var candidateWorld = targetPos + _activeEntry.Offset;

            var vp = _cam.WorldToViewportPoint(candidateWorld);
            var distance = vp.z;
            var frustumHeight = 2f * distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var frustumWidth = frustumHeight * _cam.aspect;

            var marginX = TooltipWorldHalfWidth / frustumWidth + Padding;
            var marginY = TooltipWorldHalfHeight / frustumHeight + Padding;

            var clampedX = Mathf.Clamp(vp.x, marginX, 1f - marginX);
            var clampedY = Mathf.Clamp(vp.y, marginY, 1f - marginY);

            if (Mathf.Abs(clampedX - vp.x) > 0.001f || Mathf.Abs(clampedY - vp.y) > 0.001f)
            {
                candidateWorld += _cam.transform.right * ((clampedX - vp.x) * frustumWidth)
                                + _cam.transform.up * ((clampedY - vp.y) * frustumHeight);
            }

            t.position = candidateWorld;
            t.LookAt(t.position + _cam.transform.forward);
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
        }
    }
}
