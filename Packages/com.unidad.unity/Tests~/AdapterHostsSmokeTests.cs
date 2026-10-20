using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unidad.Audio;
using Unidad.Core.Diagnostics;
using Unidad.Core.EventBus;
using Unidad.Flow;
using Unidad.Save;
using Unidad.Text;

namespace Unidad.Adapter.Smoke
{
    public sealed class AdapterHostsSmokeTests
    {
        [Test]
        public void FlowMirror_LoadingToPlay_LiftsSealsUnpauses()
        {
            var steps = FlowMirrorPlan.For(new FlowStateChangedEvent(FlowState.Loading, FlowState.Play), menuVisible: false);
            Assert.That(steps.Select(s => s.Kind), Is.EqualTo(new[]
                { FlowMirrorStepKind.MenuHide, FlowMirrorStepKind.LiftCurtain, FlowMirrorStepKind.Seal, FlowMirrorStepKind.Pause }));
            Assert.That(steps[0].Cue, Is.Null);
            Assert.That(steps[3].Paused, Is.False);
        }

        [Test]
        public void FlowMirror_PauseOpenCueOnlyFromPlay_CloseCueOnlyWhenShown()
        {
            var paused = FlowMirrorPlan.For(new FlowStateChangedEvent(FlowState.Play, FlowState.Paused), false);
            Assert.That(paused[0], Is.EqualTo(new FlowMirrorStep(FlowMirrorStepKind.MenuPausePage, UiCues.PauseOpen)));
            Assert.That(paused[1], Is.EqualTo(new FlowMirrorStep(FlowMirrorStepKind.Pause, Paused: true)));

            var quitPage = FlowMirrorPlan.For(new FlowStateChangedEvent(FlowState.Paused, FlowState.QuitConfirm), true);
            Assert.That(quitPage[0], Is.EqualTo(new FlowMirrorStep(FlowMirrorStepKind.MenuQuitPage)));
            Assert.That(quitPage[1].Paused, Is.True);

            var back = FlowMirrorPlan.For(new FlowStateChangedEvent(FlowState.QuitConfirm, FlowState.Paused), true);
            Assert.That(back[0].Cue, Is.Null);

            var resumed = FlowMirrorPlan.For(new FlowStateChangedEvent(FlowState.Paused, FlowState.Play), true);
            Assert.That(resumed[0], Is.EqualTo(new FlowMirrorStep(FlowMirrorStepKind.MenuHide, UiCues.PauseClose)));
        }

        [Test]
        public void FlowMirror_QuitStopsBeforePauseFlag()
        {
            var steps = FlowMirrorPlan.For(new FlowStateChangedEvent(FlowState.QuitConfirm, FlowState.Quit), true);
            Assert.That(steps.Select(s => s.Kind), Is.EqualTo(new[] { FlowMirrorStepKind.MenuHide, FlowMirrorStepKind.Quit }));
        }

        [Test]
        public void PausePage_KeysAndVerbsPerState()
        {
            var flow = new FakeFlow();
            var pause = PausePage.For(FlowState.Paused).Value;
            Assert.That((pause.TitleKey, pause.First.TextKey, pause.Second.TextKey), Is.EqualTo((TextKeys.PauseTitle, TextKeys.PauseResume, TextKeys.PauseQuit)));
            var quit = PausePage.For(FlowState.QuitConfirm).Value;
            Assert.That((quit.TitleKey, quit.First.TextKey, quit.Second.TextKey), Is.EqualTo((TextKeys.QuitTitle, TextKeys.QuitStay, TextKeys.QuitConfirm)));
            Assert.That(PausePage.For(FlowState.Play), Is.Null);
            foreach (var verb in new[] { pause.First.Verb, pause.Second.Verb, quit.First.Verb, quit.Second.Verb }) PausePage.Invoke(flow, verb);
            Assert.That(flow.Calls, Is.EqualTo(new[] { "Resume", "RequestQuit", "CancelQuit", "ConfirmQuit" }));
        }

        [Test]
        public void PauseKey_BackOnlyAtOrBelowWorldFocus()
        {
            var flow = new FakeFlow();
            Assert.That(PauseKey.Press(flow, InputFocusLayer.None), Is.True);
            Assert.That(PauseKey.Press(flow, InputFocusLayer.World), Is.True);
            Assert.That(PauseKey.Press(flow, InputFocusLayer.Modal), Is.False);
            Assert.That(flow.Calls, Is.EqualTo(new[] { "Back", "Back" }));
        }

        [Test]
        public void CurtainTimeline_LiftFinishesOnce_CoverCancelsALateFinish()
        {
            Assert.That(CurtainTimeline.GuardMilliseconds, Is.EqualTo(450));
            Assert.That(CurtainTimeline.PulseMilliseconds, Is.EqualTo(900));
            var curtain = new CurtainTimeline();
            int first = curtain.Lift();
            curtain.Cover();
            Assert.That(curtain.Finish(first), Is.False, "a scene switch covered mid-fade: the old lift's guard must not hide it");
            Assert.That(curtain.State, Is.EqualTo(CurtainState.Covered));
            int second = curtain.Lift();
            Assert.That(curtain.Finish(second), Is.True);
            Assert.That(curtain.Finish(second), Is.False, "transition end and guard both fire: once only");
            Assert.That(curtain.State, Is.EqualTo(CurtainState.Gone));
        }

        [Test]
        public void SaveBoot_FreshStartsFresh_DeferredWaits_ElseLoads()
        {
            Assert.That(SaveBoot.Decide(fresh: false, deferred: false), Is.EqualTo(SaveBootStep.Load));
            Assert.That(SaveBoot.Decide(fresh: false, deferred: true), Is.EqualTo(SaveBootStep.Wait));
            Assert.That(SaveBoot.Decide(fresh: true, deferred: false), Is.EqualTo(SaveBootStep.StartFresh));
            Assert.That(SaveBoot.Decide(fresh: true, deferred: true), Is.EqualTo(SaveBootStep.StartFresh));
        }

        [Test]
        public void SaveNotice_LinePerOutcome()
        {
            var text = new FakeText(new Dictionary<string, string> { [TextKeys.SaveRefused] = "v{0}", [TextKeys.SaveUnreadable] = "bad" });
            Assert.That(SaveNotice.Line(new SaveLoadResult(SaveLoadOutcome.RefusedFutureVersion, 3, ""), text), Is.EqualTo("v3"));
            Assert.That(SaveNotice.Line(new SaveLoadResult(SaveLoadOutcome.Unreadable, 0, ""), text), Is.EqualTo("bad"));
            Assert.That(SaveNotice.Line(new SaveLoadResult(SaveLoadOutcome.Loaded, 1, ""), text), Is.Null);
            Assert.That(SaveNotice.Line(new SaveLoadResult(SaveLoadOutcome.NoSave, 0, ""), text), Is.Null);
        }

        [Test]
        public void StaticHook_QuitHandlerDetachedOnce()
        {
            Func<bool> wantsToQuit = null;
            var hook = new StaticHook<Func<bool>>(h => wantsToQuit += h, h => wantsToQuit -= h, () => true);
            hook.Attach();
            hook.Attach();
            Assert.That(wantsToQuit.GetInvocationList(), Has.Length.EqualTo(1));
            hook.Detach();
            hook.Detach();
            Assert.That(wantsToQuit, Is.Null);
        }

        [Test]
        public void BootFailureText_FallsBackWhenTextMissingOrThrows()
        {
            Assert.That(BootFailureText.Title(null), Is.EqualTo(BootFailureText.FallbackTitle));
            Assert.That(BootFailureText.Title(() => null), Is.EqualTo(BootFailureText.FallbackTitle));
            Assert.That(BootFailureText.Title(() => throw new InvalidOperationException()), Is.EqualTo(BootFailureText.FallbackTitle));
            Assert.That(BootFailureText.Title(() => new FakeText(new Dictionary<string, string>())), Is.EqualTo(BootFailureText.FallbackTitle));
            Assert.That(BootFailureText.Title(() => new FakeText(new Dictionary<string, string> { [TextKeys.BootFailed] = "Nope" })), Is.EqualTo("Nope"));
        }

        [Test]
        public void MusicFade_CrossfadeAndZeroLengthSettle()
        {
            var half = MusicFade.Settle(MusicFade.Progress(0.6f, 1.2f), risingTarget: 0.8f, fallingFrom: 1f);
            Assert.That(half.Rising, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(half.Falling, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(half.Done, Is.False);
            var instant = MusicFade.Settle(MusicFade.Progress(0f, 0f), 0.8f, 1f);
            Assert.That((instant.Rising, instant.Falling, instant.Done), Is.EqualTo((0.8f, 0f, true)));
            Assert.That(MusicFade.Settle(3f, 0.5f, 0.5f).Rising, Is.EqualTo(0.5f), "t is clamped");
        }

        [Test]
        public void BusMix_VolumeIsBaseTimesBusTimesMaster_MuteZero()
        {
            var levels = new[] { 0.5f, 1f, 0.8f, 1f, 1f };
            Assert.That(BusMix.Volume(0.5f, AudioBus.Sfx, levels, muted: false), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(BusMix.Volume(1f, AudioBus.Master, levels, muted: false), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(BusMix.Volume(1f, AudioBus.Sfx, levels, muted: true), Is.EqualTo(0f));
        }

        [Test]
        public void ActiveVoices_FinishedVoicesReturnToThePoolAndAreReused()
        {
            var pool = new Queue<object>(new[] { new object() });
            var active = new ActiveVoices<object>();
            var playing = new HashSet<object>();
            var first = pool.Dequeue();
            active.Add(first);
            playing.Add(first);
            Assert.That(active.Sweep(playing.Contains, pool.Enqueue), Is.EqualTo(0));
            Assert.That(pool, Is.Empty, "a playing voice stays out");
            playing.Clear();
            Assert.That(active.Sweep(playing.Contains, pool.Enqueue), Is.EqualTo(1));
            Assert.That(pool.Dequeue(), Is.SameAs(first));
            Assert.That(active.Count, Is.EqualTo(0));
        }

        [Test]
        public void DebugModeState_TogglePublishesOnce_HideFlips()
        {
            var bus = new FakeBus();
            var state = new DebugModeState(bus);
            state.Activate();
            state.Activate();
            state.ToggleWindowsHidden();
            state.Toggle();
            Assert.That(bus.Published, Is.EqualTo(new object[]
                { new DebugModeActivatedEvent(), new CloseAllDebugWindowsEvent(true), new DebugModeDeactivatedEvent() }));
            Assert.That((state.IsActive, state.WindowsHidden), Is.EqualTo((false, true)));
        }

        [Test]
        public void DebugLayout_GroupsByTitleInOrder()
        {
            var tabs = DebugLayout.Group(new[] { new Section("B", 10), new Section("A", 0), new Section("B", 5) });
            Assert.That(tabs.Select(t => t.Title), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(tabs[1].Sections.Select(s => s.Order), Is.EqualTo(new[] { 5, 10 }));
        }

        [Test]
        public void HistoryDebugSection_WithoutHistorySaysHowToTurnItOn()
        {
            var ui = new FakeDebugUi();
            new HistoryDebugSection(null).Build(ui);
            Assert.That(ui.Readouts, Is.EqualTo(new[] { "History: off (launch with history=true)" }));
        }

        private sealed class Section : IDebugSection
        {
            public Section(string title, int order) => (Title, Order) = (title, order);
            public string Title { get; }
            public int Order { get; }
            public void Build(IDebugUi ui) { }
        }

        private sealed class FakeDebugUi : IDebugUi
        {
            public List<string> Readouts { get; } = new();
            public void Button(string text, Action action) { }
            public void Readout(string label, Func<string> value) => Readouts.Add($"{label}: {value()}");
            public void IntField(string label, int value, Action<int> set) { }
            public void TextField(string label, string value, Action<string> set) { }
            public void Choice(string label, string[] options, Action<int> pick) { }
            public void Separator() { }
        }

        private sealed class FakeFlow : IFlowService
        {
            public List<string> Calls { get; } = new();
            public FlowState State => FlowState.Play;
            public bool AcceptsGameplayInput => true;
            public void Start(IWorldReadiness readiness) => Calls.Add("Start");
            public void Resume() => Calls.Add("Resume");
            public void Back() => Calls.Add("Back");
            public void RequestQuit() => Calls.Add("RequestQuit");
            public void CancelQuit() => Calls.Add("CancelQuit");
            public void ConfirmQuit() => Calls.Add("ConfirmQuit");
            public void Tick(float deltaTime) { }
        }

        private sealed class FakeText : ITextService
        {
            private readonly Dictionary<string, string> _table;
            public FakeText(Dictionary<string, string> table) => _table = table;
            public string Get(string key) => _table.TryGetValue(key, out var v) ? v : "[" + key + "]";
            public string Format(string key, params object[] args) => string.Format(Get(key), args);
            public bool Has(string key) => _table.ContainsKey(key);
        }

        private sealed class FakeBus : IEventBus
        {
            public List<object> Published { get; } = new();
            public IDisposable Subscribe<T>(Action<T> handler) where T : struct => throw new NotSupportedException();
            public void Publish<T>(T eventData) where T : struct => Published.Add(eventData);
            public void Unsubscribe<T>(Action<T> handler) where T : struct { }
            public void ClearAllSubscriptions() { }
        }
    }
}
