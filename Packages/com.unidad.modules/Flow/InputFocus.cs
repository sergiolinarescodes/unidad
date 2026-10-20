namespace Unidad.Flow
{
    /// <summary>The framework's focus layers; a game adds its own numbers between them.</summary>
    public static class InputFocusLayer
    {
        public const int None = 0, World = 10, Modal = 200;
    }

    public readonly record struct InputFocusChangedEvent(int From, int To);

    /// <summary>Which layer owns input: the highest active one.</summary>
    public interface IInputFocusService
    {
        int CurrentFocus { get; }

        /// <summary>False for <see cref="InputFocusLayer.None"/>; true when the layer is now the highest.</summary>
        bool RequestFocus(int layer);

        void ReleaseFocus(int layer);

        /// <summary>CurrentFocus == layer.</summary>
        bool HasFocus(int layer);

        /// <summary>CurrentFocus &gt; layer.</summary>
        bool IsBlocked(int layer);
    }

    public static class FlowInput
    {
        /// <summary>The pause key's Back is honoured only at or below the world layer (Esc is a higher layer's cancel).</summary>
        public static bool BackAllowed(int currentFocus) => currentFocus <= InputFocusLayer.World;
    }
}
