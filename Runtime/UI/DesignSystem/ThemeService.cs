using Unidad.Core.EventBus;
using Unidad.Core.Systems;
using Unidad.Core.UI.Events;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.DesignSystem
{
    internal sealed class ThemeService : SystemServiceBase, IThemeService
    {
        public string CurrentTheme { get; private set; } = "default";
        public StyleSheet ThemeStyleSheet { get; private set; }
        public StyleSheet ComponentStyleSheet { get; private set; }

        public ThemeService(
            IEventBus eventBus,
            StyleSheet themeSheet = null,
            StyleSheet componentSheet = null) : base(eventBus)
        {
            ThemeStyleSheet = themeSheet;
            ComponentStyleSheet = componentSheet;
        }

        public void SetTheme(string themeName, StyleSheet themeSheet)
        {
            if (ThemeStyleSheet != null && ThemeStyleSheet != themeSheet)
            {
                // Existing theme will be swapped by consumers via ThemeChangedEvent
            }

            CurrentTheme = themeName;
            ThemeStyleSheet = themeSheet;
            Publish(new ThemeChangedEvent(themeName));
        }

        public void ApplyTo(VisualElement root)
        {
            if (root == null) return;

            if (ThemeStyleSheet != null && !root.styleSheets.Contains(ThemeStyleSheet))
                root.styleSheets.Add(ThemeStyleSheet);

            if (ComponentStyleSheet != null && !root.styleSheets.Contains(ComponentStyleSheet))
                root.styleSheets.Add(ComponentStyleSheet);
        }
    }
}
