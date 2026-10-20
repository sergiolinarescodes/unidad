using System;
using UnityEngine.InputSystem;

namespace Unidad.Adapter
{
    /// <summary>Alt+D (<c>debug_toggle</c>) toggles the debug mode; Alt+Q (<c>debug_hide</c>) hides or shows the debug windows.</summary>
    internal sealed class DebugInputBridge : IDisposable
    {
        private readonly InputAction _toggle = new("debug_toggle", InputActionType.Button);
        private readonly InputAction _hide = new("debug_hide", InputActionType.Button);

        public DebugInputBridge(DebugModeState state)
        {
            _toggle.AddCompositeBinding("OneModifier").With("Modifier", "<Keyboard>/alt").With("Binding", "<Keyboard>/d");
            _hide.AddCompositeBinding("OneModifier").With("Modifier", "<Keyboard>/alt").With("Binding", "<Keyboard>/q");
            _toggle.performed += _ => state.Toggle();
            _hide.performed += _ => state.ToggleWindowsHidden();
            _toggle.Enable();
            _hide.Enable();
        }

        public void Dispose()
        {
            foreach (var action in new[] { _toggle, _hide })
            {
                action.Disable();
                action.Dispose();
            }
        }
    }
}
