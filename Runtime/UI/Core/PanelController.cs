using UnityEngine.UIElements;

namespace Unidad.Core.UI.Core
{
    public abstract class PanelController<TModel> : IPanelController
    {
        public abstract string PanelId { get; }
        public abstract UILayer Layer { get; }
        public bool IsVisible { get; private set; }
        public VisualElement Root { get; private set; }

        protected TModel Model { get; private set; }

        public void Show(object model = null)
        {
            if (model is TModel typedModel)
                Model = typedModel;

            Root ??= CreateRoot();
            OnBeforeShow(Model);
            Root.style.display = DisplayStyle.Flex;
            IsVisible = true;
            OnAfterShow(Model);
        }

        public void Hide()
        {
            if (!IsVisible) return;
            OnBeforeHide();
            Root.style.display = DisplayStyle.None;
            IsVisible = false;
            OnAfterHide();
        }

        public virtual void OnLayerChanged(UILayer layer) { }

        protected abstract VisualElement CreateRoot();
        protected virtual void OnBeforeShow(TModel model) { }
        protected virtual void OnAfterShow(TModel model) { }
        protected virtual void OnBeforeHide() { }
        protected virtual void OnAfterHide() { }

        public virtual void Dispose()
        {
            Root?.RemoveFromHierarchy();
        }
    }
}
