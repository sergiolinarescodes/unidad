using System;
using System.Collections.Generic;
using System.Linq;
using Unidad.Adapter.Ui;
using Unidad.Core.DI;
using Unidad.Core.Diagnostics;
using Unidad.Core.EventBus;
using Unidad.Core.Ticking;
using Unidad.Flow;
using Unidad.Launch;
using Unidad.Save;
using Unidad.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Adapter
{
    /// <summary>The save's Unity half: registers participants, ticks the save and autosave, saves on quit, shows the refusal notice.</summary>
    internal sealed class SaveHost : MonoBehaviour
    {
        private IAutosave _autosave;
        private IPanelLibrary _panels;
        private ITextService _text;
        private UiDocumentHost _notice;
        private StaticHook<Func<bool>> _wantsToQuit;
        private readonly List<IDisposable> _subscriptions = new();

        public static SaveHost Bind(Container container, IGameObjectFactory factory, ITickRegistry ticks, IPanelLibrary panels,
            IReadOnlyList<ISaveParticipant> participants, LaunchArgs launch)
        {
            var save = container.Resolve<ISaveService>();
            var autosave = container.Resolve<IAutosave>();
            var flow = container.Resolve<IFlowService>();
            foreach (var participant in participants) save.Register(participant);

            var host = factory.Create<SaveHost>("Save");
            host._autosave = autosave;
            host._panels = panels;
            host._text = container.TryResolve<ITextService>(out var text) ? text : null;
            host._subscriptions.Add(container.Resolve<IEventBus>().Subscribe<GameLoadedEvent>(loaded => host.Loaded(loaded.Result)));
            host._subscriptions.Add(autosave.QuitOn<FlowStateChangedEvent>(change => change.To == FlowState.Quit));
            var tuning = container.TryResolve<AutosaveTuning>(out var bound) ? bound : new AutosaveTuning();
            if (tuning.PeriodSeconds > 0) host._subscriptions.Add(autosave.PeriodicWhile(() => flow.State == FlowState.Play));
            ticks.Register(save, TickGroup.Simulation);
            ticks.Register(autosave, TickGroup.Simulation);
            host._wantsToQuit = new StaticHook<Func<bool>>(h => Application.wantsToQuit += h, h => Application.wantsToQuit -= h, host.OnWantsToQuit);
            host._wantsToQuit.Attach();

            Log.Info($"[Save] {participants.Count} participant(s)" + (participants.Count > 0 ? ": " + string.Join(", ", participants.Select(p => p.Key)) : "."));
            switch (SaveBoot.Decide(launch.IsOn(SaveSettings.Fresh), container.HasBinding<SaveLoadDeferred>()))
            {
                case SaveBootStep.StartFresh: save.StartFresh(); break;
                case SaveBootStep.Load: save.Load(); break;
                default: Log.Info("[Save] Load deferred to the game."); break;
            }
            return host;
        }

        /// <summary>Every load, the game's deferred one included, logs its outcome; a refused or unreadable slot is said in the corner for the run.</summary>
        private void Loaded(SaveLoadResult result)
        {
            Log.Info($"[Save] {result.Outcome}" + (string.IsNullOrEmpty(result.Message) ? "." : $": {result.Message}"));
            var line = _text == null ? null : SaveNotice.Line(result, _text);
            if (line == null) return;
            _notice ??= _panels.Overlay("SaveNotice", PanelOrder.Hud, UiKeys.SaveNotice);
            _notice.Q<Label>("line").text = line;
        }

        private bool OnWantsToQuit()
        {
            _autosave?.Quit();
            return true;
        }

        private void OnApplicationQuit() => _autosave?.Quit();

        private void OnDestroy()
        {
            _autosave?.Quit();
            _autosave = null;
            _wantsToQuit?.Detach();
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
    }
}
