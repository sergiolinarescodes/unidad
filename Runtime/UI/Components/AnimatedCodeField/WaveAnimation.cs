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

        public void Initialize(int totalChars) { }

        public bool Update(float elapsed, int charIndex, int totalChars, Label charLabel)
        {
            if (elapsed > Duration) return false;

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
