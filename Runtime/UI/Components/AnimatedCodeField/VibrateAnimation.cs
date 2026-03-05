using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    internal sealed class VibrateAnimation : ICharAnimation
    {
        public float Amplitude = 3f;
        public float Frequency = 18f;
        public float Duration = 0.3f;

        private readonly HashSet<int> _targetIndices = new();

        public void SetTargetIndices(IEnumerable<int> indices)
        {
            _targetIndices.Clear();
            foreach (var i in indices)
                _targetIndices.Add(i);
        }

        public void Initialize(int totalChars) { }

        public bool Update(float elapsed, int charIndex, int totalChars, Label charLabel)
        {
            if (elapsed > Duration) return false;

            if (!_targetIndices.Contains(charIndex))
                return true; // still running, but this char isn't a target

            float t = elapsed / Duration;
            float fade = 1f - t * t;
            float offsetX = Amplitude * fade * Mathf.Sin(elapsed * Frequency * 2f * Mathf.PI);
            float offsetY = Amplitude * fade * Mathf.Cos(elapsed * Frequency * 2f * Mathf.PI * 0.7f);
            charLabel.style.translate = new Translate(offsetX, offsetY);
            return true;
        }

        public void Reset(Label charLabel)
        {
            charLabel.style.translate = new Translate(0, 0);
        }
    }
}
