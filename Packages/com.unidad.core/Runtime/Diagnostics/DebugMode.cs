namespace Unidad.Core.Diagnostics;

/// <summary>The Alt+D debug mode turned on.</summary>
public readonly record struct DebugModeActivatedEvent;

/// <summary>The debug mode turned off.</summary>
public readonly record struct DebugModeDeactivatedEvent;

/// <summary>Alt+Q: every debug window hides (<paramref name="Hidden"/> true) or shows again.</summary>
public readonly record struct CloseAllDebugWindowsEvent(bool Hidden);

/// <summary>Whether the debug mode is on, for any view that follows it. The adapter's debug window binds the real one and publishes the events.</summary>
public interface IDebugModeState
{
    bool IsActive { get; }
    bool WindowsHidden { get; }
}

/// <summary>The debug mode of a scene that has no debug window: always off.</summary>
internal sealed class InactiveDebugMode : IDebugModeState
{
    public bool IsActive => false;
    public bool WindowsHidden => false;
}
