using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    [UxmlElement]
    public partial class UnidadProgressBar : VisualElement
    {
        private readonly VisualElement _fill;
        private float _value;

        public float Value
        {
            get => _value;
            set
            {
                _value = Mathf.Clamp01(value);
                _fill.style.width = Length.Percent(_value * 100f);
            }
        }

        public UnidadProgressBar()
        {
            AddToClassList("unidad-progress");

            _fill = new VisualElement();
            _fill.AddToClassList("unidad-progress__fill");
            _fill.style.width = Length.Percent(0);
            Add(_fill);
        }

        public UnidadProgressBar(float initialValue) : this()
        {
            Value = initialValue;
        }

        public void SetVariant(ProgressVariant variant)
        {
            _fill.RemoveFromClassList("unidad-progress__fill--success");
            _fill.RemoveFromClassList("unidad-progress__fill--warning");
            _fill.RemoveFromClassList("unidad-progress__fill--danger");

            var className = variant switch
            {
                ProgressVariant.Success => "unidad-progress__fill--success",
                ProgressVariant.Warning => "unidad-progress__fill--warning",
                ProgressVariant.Danger => "unidad-progress__fill--danger",
                _ => null
            };

            if (className != null)
                _fill.AddToClassList(className);
        }
    }

    public enum ProgressVariant
    {
        Default,
        Success,
        Warning,
        Danger
    }
}
