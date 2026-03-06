using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    internal sealed class WaveAnimation : ICharAnimation
    {
        public float Amplitude = 4f;
        public float Frequency = 2f;
        public float CharDelay = 0.15f;
        public float Duration = 1.5f;

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

            if (_targetIndices.Count > 0 && !_targetIndices.Contains(charIndex))
                return true; // still running, but this char isn't a target

            float t = elapsed / Duration;
            float fade = 1f - t * t; // quadratic fade-out
            float y = Amplitude * fade * Mathf.Sin(elapsed * Frequency * 2f * Mathf.PI + charIndex * CharDelay);
            charLabel.style.translate = new Translate(0, y);
            return true;
        }

        public void Reset(Label charLabel)
        {
            charLabel.style.translate = new Translate(0, 0);
        }
    }
}
