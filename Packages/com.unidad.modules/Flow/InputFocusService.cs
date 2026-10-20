using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;

namespace Unidad.Flow
{
    /// <summary>A set of active layers; the highest one has focus.</summary>
    internal sealed class InputFocusService : SystemServiceBase, IInputFocusService
    {
        private readonly HashSet<int> _active = new();

        public InputFocusService(IEventBus eventBus) : base(eventBus) { }

        public int CurrentFocus { get; private set; } = InputFocusLayer.None;

        public bool RequestFocus(int layer)
        {
            if (layer == InputFocusLayer.None) return false;
            _active.Add(layer);
            Update();
            return CurrentFocus == layer;
        }

        public void ReleaseFocus(int layer)
        {
            if (layer == InputFocusLayer.None) return;
            _active.Remove(layer);
            Update();
        }

        public bool HasFocus(int layer) => CurrentFocus == layer;

        public bool IsBlocked(int layer) => CurrentFocus > layer;

        private void Update()
        {
            int highest = InputFocusLayer.None;
            foreach (int layer in _active)
                if (layer > highest) highest = layer;
            if (highest == CurrentFocus) return;
            int from = CurrentFocus;
            CurrentFocus = highest;
            Publish(new InputFocusChangedEvent(from, highest));
        }
    }
}
