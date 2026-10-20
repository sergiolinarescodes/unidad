using System.Collections.Generic;
using Unidad.Audio;
using Unidad.Flow;

namespace Unidad.Adapter
{
    internal enum FlowMirrorStepKind
    {
        MenuPausePage,
        MenuQuitPage,
        MenuHide,
        LiftCurtain,
        Seal,
        Quit,
        Pause,
    }

    /// <summary>One step of the flow host's answer to a state change: <see cref="Cue"/> plays with it, <see cref="Paused"/> is the Pause step's flag.</summary>
    internal readonly record struct FlowMirrorStep(FlowMirrorStepKind Kind, SoundId? Cue = null, bool Paused = false);

    /// <summary>What the flow host does for one <see cref="FlowStateChangedEvent"/>, in order; the host only executes it.</summary>
    internal static class FlowMirrorPlan
    {
        public static IReadOnlyList<FlowMirrorStep> For(FlowStateChangedEvent change, bool menuVisible)
        {
            var steps = new List<FlowMirrorStep>(4);
            switch (change.To)
            {
                case FlowState.Paused:
                    steps.Add(new FlowMirrorStep(FlowMirrorStepKind.MenuPausePage, change.From == FlowState.Play ? UiCues.PauseOpen : null));
                    break;
                case FlowState.QuitConfirm:
                    steps.Add(new FlowMirrorStep(FlowMirrorStepKind.MenuQuitPage));
                    break;
                default:
                    steps.Add(new FlowMirrorStep(FlowMirrorStepKind.MenuHide, menuVisible && change.To == FlowState.Play ? UiCues.PauseClose : null));
                    break;
            }
            if (change.From == FlowState.Loading) steps.Add(new FlowMirrorStep(FlowMirrorStepKind.LiftCurtain));
            if (change.To == FlowState.Play) steps.Add(new FlowMirrorStep(FlowMirrorStepKind.Seal));
            if (change.To == FlowState.Quit)
            {
                steps.Add(new FlowMirrorStep(FlowMirrorStepKind.Quit));
                return steps;
            }
            steps.Add(new FlowMirrorStep(FlowMirrorStepKind.Pause, Paused: change.To is FlowState.Paused or FlowState.QuitConfirm));
            return steps;
        }
    }
}
