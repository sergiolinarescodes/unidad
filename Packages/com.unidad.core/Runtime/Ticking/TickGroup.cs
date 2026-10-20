namespace Unidad.Core.Ticking
{
    /// <summary>
    /// The phases of one frame, run in declaration order. Within a group, tickables run in the
    /// order they were registered. A scene names the group of every tickable it registers, so the
    /// frame's order is written down instead of being whatever order Register happened to be called.
    /// </summary>
    public enum TickGroup
    {
        /// <summary>Read devices and turn them into commands; main-thread job results land here first.</summary>
        Input = 0,

        /// <summary>Rules and state: movement, interactions, timers. The default group.</summary>
        Simulation = 1,

        /// <summary>Load and unload around the viewer: chunks, pages, entities.</summary>
        Streaming = 2,

        /// <summary>Views and HUD mirroring the state the groups above produced.</summary>
        Presentation = 3,

        /// <summary>Render-side work that needs the final camera and scene for this frame.</summary>
        Rendering = 4,

        /// <summary>Fixed-timestep work (physics), stepped once per fixed update with the fixed delta.</summary>
        Fixed = 5,
    }
}
