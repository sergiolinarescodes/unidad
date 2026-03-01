using System;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    [UxmlElement]
    public partial class UnidadButton : Button
    {
        private bool _isDisabled;
        private bool _isLoading;

        public event Action Clicked;

        public bool IsDisabled
        {
            get => _isDisabled;
            set
            {
                _isDisabled = value;
                EnableInClassList("unidad-button--disabled", value);
                SetEnabled(!value && !_isLoading);
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                EnableInClassList("unidad-button--loading", value);
                SetEnabled(!value && !_isDisabled);
            }
        }

        public UnidadButton() : base()
        {
            AddToClassList("unidad-button");
            text = "";

            RegisterCallback<PointerDownEvent>(_ =>
            {
                if (!_isDisabled && !_isLoading)
                    AddToClassList("unidad-button--pressed");
            });

            RegisterCallback<PointerUpEvent>(_ =>
            {
                RemoveFromClassList("unidad-button--pressed");
            });

            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                RemoveFromClassList("unidad-button--pressed");
            });

            clicked += () =>
            {
                if (!_isDisabled && !_isLoading)
                    Clicked?.Invoke();
            };
        }

        public UnidadButton(string label, Action onClick = null) : this()
        {
            text = label;
            if (onClick != null)
                Clicked += onClick;
        }

        public void SetVariant(ButtonVariant variant)
        {
            RemoveFromClassList("unidad-button--secondary");
            RemoveFromClassList("unidad-button--success");
            RemoveFromClassList("unidad-button--warning");
            RemoveFromClassList("unidad-button--danger");

            var className = variant switch
            {
                ButtonVariant.Secondary => "unidad-button--secondary",
                ButtonVariant.Success => "unidad-button--success",
                ButtonVariant.Warning => "unidad-button--warning",
                ButtonVariant.Danger => "unidad-button--danger",
                _ => null
            };

            if (className != null)
                AddToClassList(className);
        }
    }

    public enum ButtonVariant
    {
        Primary,
        Secondary,
        Success,
        Warning,
        Danger
    }
}
