using System;
using Unidad.Flow;
using Unidad.Text;

namespace Unidad.Adapter
{
    internal enum PauseVerb
    {
        Resume,
        RequestQuit,
        CancelQuit,
        ConfirmQuit,
    }

    internal readonly record struct PauseButton(string TextKey, PauseVerb Verb);

    /// <summary>The pause card's page for a flow state: a title and two buttons, each a flow verb.</summary>
    internal readonly record struct PausePage(string TitleKey, PauseButton First, PauseButton Second)
    {
        public static PausePage? For(FlowState state) => state switch
        {
            FlowState.Paused => new PausePage(TextKeys.PauseTitle,
                new PauseButton(TextKeys.PauseResume, PauseVerb.Resume), new PauseButton(TextKeys.PauseQuit, PauseVerb.RequestQuit)),
            FlowState.QuitConfirm => new PausePage(TextKeys.QuitTitle,
                new PauseButton(TextKeys.QuitStay, PauseVerb.CancelQuit), new PauseButton(TextKeys.QuitConfirm, PauseVerb.ConfirmQuit)),
            _ => null,
        };

        public static void Invoke(IFlowService flow, PauseVerb verb)
        {
            switch (verb)
            {
                case PauseVerb.Resume: flow.Resume(); break;
                case PauseVerb.RequestQuit: flow.RequestQuit(); break;
                case PauseVerb.CancelQuit: flow.CancelQuit(); break;
                case PauseVerb.ConfirmQuit: flow.ConfirmQuit(); break;
                default: throw new ArgumentOutOfRangeException(nameof(verb), verb, null);
            }
        }
    }

    /// <summary>The pause key: the flow's Back, honoured only while no layer above the world holds input focus.</summary>
    internal static class PauseKey
    {
        public static bool Press(IFlowService flow, int currentFocus)
        {
            if (!FlowInput.BackAllowed(currentFocus)) return false;
            flow.Back();
            return true;
        }
    }
}
