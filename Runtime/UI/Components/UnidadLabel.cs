using UnityEngine.UIElements;

namespace Unidad.Core.UI.Components
{
    [UxmlElement]
    public partial class UnidadLabel : Label
    {
        public UnidadLabel() : base()
        {
            AddToClassList("unidad-label");
            text = "";
        }

        public UnidadLabel(string text, string sizeClass = null) : this()
        {
            this.text = text;
            if (sizeClass != null)
                AddToClassList(sizeClass);
        }

        public void SetTextStyle(LabelStyle style)
        {
            RemoveFromClassList("unidad-label--secondary");
            RemoveFromClassList("unidad-label--muted");

            switch (style)
            {
                case LabelStyle.Secondary:
                    AddToClassList("unidad-label--secondary");
                    break;
                case LabelStyle.Muted:
                    AddToClassList("unidad-label--muted");
                    break;
            }
        }

        public void SetSize(LabelSize size)
        {
            RemoveFromClassList("unidad-label--xs");
            RemoveFromClassList("unidad-label--sm");
            RemoveFromClassList("unidad-label--lg");
            RemoveFromClassList("unidad-label--xl");
            RemoveFromClassList("unidad-label--xxl");

            var className = size switch
            {
                LabelSize.ExtraSmall => "unidad-label--xs",
                LabelSize.Small => "unidad-label--sm",
                LabelSize.Large => "unidad-label--lg",
                LabelSize.ExtraLarge => "unidad-label--xl",
                LabelSize.DoubleExtraLarge => "unidad-label--xxl",
                _ => null
            };

            if (className != null)
                AddToClassList(className);
        }
    }

    public enum LabelStyle
    {
        Primary,
        Secondary,
        Muted
    }

    public enum LabelSize
    {
        Default,
        ExtraSmall,
        Small,
        Large,
        ExtraLarge,
        DoubleExtraLarge
    }
}
