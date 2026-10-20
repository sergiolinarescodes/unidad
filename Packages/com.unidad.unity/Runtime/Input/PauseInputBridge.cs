using System;
using Unidad.Flow;
using UnityEngine.InputSystem;

namespace Unidad.Adapter
{
    /// <summary>
    /// The pause action (Escape, or Start on a pad) as the flow's Back. Input System callbacks run in its own update, so the
    /// key works while paused; a layer above the world holding focus keeps Escape as its own cancel.
    /// </summary>
    internal sealed class PauseInputBridge : IDisposable
    {
        private readonly InputAction _pause = new("pause", InputActionType.Button);

        public PauseInputBridge(IFlowService flow, IInputFocusService focusOrNull)
        {
            _pause.AddBinding("<Keyboard>/escape");
            _pause.AddBinding("<Gamepad>/start");
            _pause.performed += _ => PauseKey.Press(flow, focusOrNull?.CurrentFocus ?? InputFocusLayer.None);
            _pause.Enable();
        }

        public void Dispose()
        {
            _pause.Disable();
            _pause.Dispose();
        }
    }
}
