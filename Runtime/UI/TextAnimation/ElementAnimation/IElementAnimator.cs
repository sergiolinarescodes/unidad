using System;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.TextAnimation.ElementAnimation
{
    public interface IElementAnimator
    {
        IDisposable Animate(VisualElement target, ElementAnimationConfig config, Action onComplete = null);
        void Cancel(VisualElement target);
    }
}
