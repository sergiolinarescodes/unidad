using Unidad.Adapter.Ui;
using Unidad.Audio;
using Unidad.Flow;
using Unidad.Text;
using UnityEngine.UIElements;

namespace Unidad.Adapter
{
    /// <summary>
    /// The pause card and the quit question, mirroring the flow: shows a <see cref="PausePage"/> with its first button focused,
    /// or hides. A button plays the click and calls its flow verb; what happens next is the flow's.
    /// </summary>
    internal sealed class PauseMenu
    {
        private readonly UiDocumentHost _host;
        private readonly ITextService _text;
        private readonly ISoundService _sound;
        private readonly Label _title;
        private readonly Button _first, _second;
        private PausePage _page;

        public PauseMenu(UiDocumentHost host, IFlowService flow, ITextService text, ISoundService sound)
        {
            _host = host;
            _text = text;
            _sound = sound;
            _title = host.Q<Label>("title");
            _first = host.Q<Button>("first");
            _second = host.Q<Button>("second");
            _first.clicked += () => Press(flow, _page.First.Verb);
            _second.clicked += () => Press(flow, _page.Second.Verb);
            _host.SetVisible(false);
        }

        public bool Visible { get; private set; }

        public void Show(PausePage page, SoundId? cue)
        {
            _page = page;
            _title.text = _text.Get(page.TitleKey);
            _first.text = _text.Get(page.First.TextKey);
            _second.text = _text.Get(page.Second.TextKey);
            _host.SetVisible(true);
            Visible = true;
            if (cue is { } open) _sound.PlayUi(open);
            // Focus once the card is laid out: an element that was display:none this frame cannot take it yet.
            _first.schedule.Execute(() => _first.Focus());
        }

        public void Hide(SoundId? cue)
        {
            if (cue is { } close) _sound.PlayUi(close);
            _host.SetVisible(false);
            Visible = false;
        }

        private void Press(IFlowService flow, PauseVerb verb)
        {
            _sound.PlayUi(UiCues.Click);
            PausePage.Invoke(flow, verb);
        }
    }
}
