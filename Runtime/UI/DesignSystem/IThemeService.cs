using UnityEngine.UIElements;

namespace Unidad.Core.UI.DesignSystem
{
    public interface IThemeService
    {
        string CurrentTheme { get; }
        StyleSheet ThemeStyleSheet { get; }
        StyleSheet ComponentStyleSheet { get; }
        void SetTheme(string themeName, StyleSheet themeSheet);
        void ApplyTo(VisualElement root);
    }
}
