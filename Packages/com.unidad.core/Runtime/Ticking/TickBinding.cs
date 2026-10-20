using System;
using Unidad.Core.Abstractions;

namespace Unidad.Core.Ticking;

/// <summary>
/// One service the scene ticks, added with <c>AddMany&lt;TickBinding&gt;</c>; the adapter's StageBootstrap registers every one on
/// the TickRunner. Groups: input bridges Input; rule timelines, executor, playback, flow timers Simulation; cue lifetimes and
/// views Presentation. <paramref name="RateHz"/> 0 ticks every frame. A <paramref name="RunWhilePaused"/> binding keeps ticking
/// while the game is paused and always receives the unscaled delta.
/// </summary>
public sealed record TickBinding(ITickable Tickable, TickGroup Group, float RateHz = 0f, bool RunWhilePaused = false)
{
    public ITickable Tickable { get; } = Tickable ?? throw new ArgumentNullException(nameof(Tickable));
}
