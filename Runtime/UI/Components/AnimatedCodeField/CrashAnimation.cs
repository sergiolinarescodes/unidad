using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    internal sealed class CrashAnimation : ICharAnimation
    {
        public float ScatterDuration = 0.15f;
        public float ReturnDuration = 0.35f;
        public float ScatterRadius = 20f;

        private readonly HashSet<int> _targetIndices = new();
        private readonly Dictionary<int, Vector2> _scatterOffsets = new();
        private readonly Dictionary<Label, Color> _originalColors = new();

        public void SetTargetIndices(IEnumerable<int> indices)
        {
            _targetIndices.Clear();
            foreach (var i in indices)
                _targetIndices.Add(i);
        }

        public void Initialize(int totalChars)
        {
            _scatterOffsets.Clear();
            _originalColors.Clear();

            foreach (var i in _targetIndices)
            {
                float angle = ((i * 137.5f) % 360f) * Mathf.Deg2Rad;
                float dist = ScatterRadius * (0.5f + 0.5f * ((i * 73 + 17) % 100) / 100f);
                _scatterOffsets[i] = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
            }
        }

        public bool Update(float elapsed, int charIndex, int totalChars, Label charLabel)
        {
            float totalDuration = ScatterDuration + ReturnDuration;
            if (elapsed > totalDuration) return false;

            if (!_targetIndices.Contains(charIndex))
                return true;

            if (!_originalColors.ContainsKey(charLabel))
                _originalColors[charLabel] = charLabel.resolvedStyle.color;

            var offset = _scatterOffsets.TryGetValue(charIndex, out var o) ? o : Vector2.zero;
            var origColor = _originalColors.TryGetValue(charLabel, out var c) ? c : Color.white;

            if (elapsed <= ScatterDuration)
            {
                float t = elapsed / ScatterDuration;
                float ease = t * t;
                charLabel.style.translate = new Translate(offset.x * ease, offset.y * ease);
                charLabel.style.color = Color.Lerp(origColor, Color.red, t);
            }
            else
            {
                float t = (elapsed - ScatterDuration) / ReturnDuration;
                float ease = 1f - (1f - t) * (1f - t);
                charLabel.style.translate = new Translate(offset.x * (1f - ease), offset.y * (1f - ease));
                charLabel.style.color = Color.Lerp(Color.red, origColor, t);
            }

            return true;
        }

        public void Reset(Label charLabel)
        {
            charLabel.style.translate = new Translate(0, 0);
            if (_originalColors.TryGetValue(charLabel, out var origColor))
                charLabel.style.color = origColor;
        }
    }
}
