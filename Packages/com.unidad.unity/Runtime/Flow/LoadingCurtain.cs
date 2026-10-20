using System;
using Unidad.Adapter.Ui;
using UnityEngine.UIElements;

namespace Unidad.Adapter
{
    /// <summary>
    /// The opaque cover over a scene that is not ready to be seen: up from the first frame with the mark pulsing, fades out
    /// over <see cref="Unidad.Flow.FlowTuning.CurtainLiftSeconds"/> on the lift, and covers again at once on a scene switch.
    /// </summary>
    internal sealed class LoadingCurtain
    {
        private const string Lifted = "unidad-curtain--lifted", MarkLow = "unidad-curtain__mark--low";

        private readonly VisualElement _cover;
        private readonly IVisualElementScheduledItem _pulse;
        private readonly CurtainTimeline _timeline = new();

        public LoadingCurtain(UiDocumentHost host)
        {
            _cover = host.Q<VisualElement>("cover");
            var mark = host.Q<VisualElement>("mark");
            _pulse = mark.schedule.Execute(() => mark.ToggleInClassList(MarkLow)).Every(CurtainTimeline.PulseMilliseconds);
            Cover();
        }

        public CurtainState State => _timeline.State;

        /// <summary>Opaque this frame (the fade transition lives on the lifted class only, so removing it is instant).</summary>
        public void Cover()
        {
            _timeline.Cover();
            _cover.RemoveFromClassList(Lifted);
            _cover.style.display = DisplayStyle.Flex;
            _pulse.Resume();
        }

        /// <summary>Fades out, then hides and calls <paramref name="gone"/>: on the fade's end or the guard, whichever first, once.</summary>
        public void Lift(Action gone = null)
        {
            int token = _timeline.Lift();
            EventCallback<TransitionEndEvent> ended = null;
            void Finish()
            {
                _cover.UnregisterCallback(ended);
                if (!_timeline.Finish(token)) return;
                _cover.style.display = DisplayStyle.None;
                _pulse.Pause();
                gone?.Invoke();
            }
            ended = e =>
            {
                if (e.target == _cover) Finish();
            };
            _cover.RegisterCallback(ended);
            _cover.schedule.Execute(Finish).ExecuteLater(CurtainTimeline.GuardMilliseconds);
            _cover.AddToClassList(Lifted);
        }
    }
}
