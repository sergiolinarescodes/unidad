namespace Unidad.Core.UI.Core
{
    public interface IUIService
    {
        T Show<T>(object model = null) where T : class, IPanelController;
        void Hide<T>() where T : class, IPanelController;
        void HideAll(UILayer layer);
        bool IsVisible<T>() where T : class, IPanelController;
        T Get<T>() where T : class, IPanelController;
        void Register(IPanelController panel);
    }
}
