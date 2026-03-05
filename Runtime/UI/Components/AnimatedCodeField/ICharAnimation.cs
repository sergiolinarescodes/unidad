using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    public interface ICharAnimation
    {
        /// <summary>Called once when animation starts.</summary>
        void Initialize(int totalChars);

        /// <summary>
        /// Called every frame. Modify charLabel styles (translate, color, opacity, etc.).
        /// Returns false when animation is complete.
        /// </summary>
        bool Update(float elapsed, int charIndex, int totalChars, Label charLabel);

        /// <summary>Called when animation is cancelled or complete — reset any modified styles.</summary>
        void Reset(Label charLabel);
    }
}
