using System;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    [UxmlElement]
    public partial class UnidadCard : VisualElement
    {
        private IElementAnimator _animator;

        public UnidadCard()
        {
            AddToClassList("unidad-card");
        }

        public void SetAnimator(IElementAnimator animator)
        {
            _animator = animator;
        }

        public void AnimateEnter(Action onComplete = null)
        {
            if (_animator == null)
            {
                onComplete?.Invoke();
                return;
            }

            AddToClassList("unidad-card--entering");
            schedule.Execute(() =>
            {
                RemoveFromClassList("unidad-card--entering");
                onComplete?.Invoke();
            }).StartingIn(300);
        }

        public void AnimateExit(Action onComplete = null)
        {
            if (_animator == null)
            {
                onComplete?.Invoke();
                return;
            }

            AddToClassList("unidad-card--exiting");
            schedule.Execute(() =>
            {
                RemoveFromClassList("unidad-card--exiting");
                onComplete?.Invoke();
            }).StartingIn(300);
        }
    }
}
