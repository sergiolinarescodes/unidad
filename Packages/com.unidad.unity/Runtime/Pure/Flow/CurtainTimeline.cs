using System;
using Unidad.Flow;

namespace Unidad.Adapter
{
    internal enum CurtainState
    {
        Covered,
        Lifting,
        Gone,
    }

    /// <summary>
    /// The curtain's states. A lift finishes once, on the fade's end or on the guard, whichever comes first; a Cover during
    /// the fade cancels it, so that lift's late finish is ignored.
    /// </summary>
    internal sealed class CurtainTimeline
    {
        /// <summary>The lift ends by this time even if the transition-end event never comes.</summary>
        public const float GuardSeconds = FlowTuning.CurtainLiftSeconds + 0.1f;

        private int _generation;

        public static long PulseMilliseconds => (long)Math.Round(FlowTuning.CurtainPulseSeconds * 1000.0);

        public static long GuardMilliseconds => (long)Math.Round(GuardSeconds * 1000.0);

        public CurtainState State { get; private set; } = CurtainState.Covered;

        public void Cover()
        {
            State = CurtainState.Covered;
            _generation++;
        }

        /// <summary>Starts a lift; the token names it for <see cref="Finish"/>.</summary>
        public int Lift()
        {
            State = CurtainState.Lifting;
            return ++_generation;
        }

        /// <summary>True only the first time for the current lift.</summary>
        public bool Finish(int token)
        {
            if (State != CurtainState.Lifting || token != _generation) return false;
            State = CurtainState.Gone;
            return true;
        }
    }
}
