using System;
using Unidad.Adapter.Ui;
using Unidad.Audio;
using Unidad.Core.DI;
using Unidad.Core.EventBus;
using Unidad.Core.Ticking;
using Unidad.Flow;
using Unidad.Text;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>The flow's Unity half: the loading curtain, the pause menu, the quit question and the time scale.</summary>
    internal sealed class FlowHost : MonoBehaviour
    {
        private LoadingCurtain _curtain;
        private PauseMenu _menu;
        private PauseInputBridge _pauseInput;
        private ScenePreparation _preparation;
        private AssetLibrary _assets;
        private ITickRegistry _ticks;
        private IAppQuit _quit;
        private SceneLoader _scenes;
        private IDisposable[] _subscriptions = Array.Empty<IDisposable>();
        private bool _paused;
        private float _savedTimeScale = 1f;

        public static FlowHost Bind(Container container, IGameObjectFactory factory, ITickRegistry ticks, IPanelLibrary panels,
            IWorldReadiness readiness, ScenePreparation preparation, AssetLibrary assets)
        {
            var flow = container.Resolve<IFlowService>();
            var bus = container.Resolve<IEventBus>();
            var host = factory.Create<FlowHost>("Flow");
            host._ticks = ticks;
            host._preparation = preparation;
            host._assets = assets;
            host._quit = container.Resolve<IAppQuit>();
            host._scenes = container.TryResolve<SceneLoader>(out var loader) ? loader : new SceneLoader();
            host._curtain = new LoadingCurtain(panels.Overlay("LoadingCurtain", PanelOrder.Curtain, UiKeys.Curtain));
            host._menu = new PauseMenu(panels.Overlay("PauseMenu", PanelOrder.Pause, UiKeys.PauseMenu), flow,
                container.Resolve<ITextService>(), container.Resolve<ISoundService>());
            host._pauseInput = new PauseInputBridge(flow, container.TryResolve<IInputFocusService>(out var focus) ? focus : null);
            host._subscriptions = new[]
            {
                bus.Subscribe<FlowStateChangedEvent>(host.Mirror),
                bus.Subscribe<SceneSwitchRequestedEvent>(host.Switch),
            };
            ticks.Register(flow, TickGroup.Simulation);
            flow.Start(new RequiredReadiness(readiness ?? ImmediateReadiness.Instance, () => preparation.IsReady));
            return host;
        }

        private void Mirror(FlowStateChangedEvent change)
        {
            foreach (var step in FlowMirrorPlan.For(change, _menu.Visible))
            {
                switch (step.Kind)
                {
                    case FlowMirrorStepKind.MenuPausePage:
                        _menu.Show(PausePage.For(FlowState.Paused).Value, step.Cue);
                        break;
                    case FlowMirrorStepKind.MenuQuitPage:
                        _menu.Show(PausePage.For(FlowState.QuitConfirm).Value, step.Cue);
                        break;
                    case FlowMirrorStepKind.MenuHide:
                        _menu.Hide(step.Cue);
                        break;
                    case FlowMirrorStepKind.LiftCurtain:
                        _curtain.Lift();
                        break;
                    case FlowMirrorStepKind.Seal:
                        if (!_assets.IsSealed) _assets.Seal();
                        break;
                    case FlowMirrorStepKind.Quit:
                        _quit.Quit();
                        break;
                    case FlowMirrorStepKind.Pause:
                        SetPaused(step.Paused);
                        break;
                }
            }
        }

        private void Switch(SceneSwitchRequestedEvent request)
        {
            _curtain.Cover();
            SetPaused(false);
            SceneTicketHolder.FindOrCreate().Put(request.Ticket);
            _preparation.Enqueue("scene:" + request.To, () => _scenes.Load(request.To));
        }

        private void SetPaused(bool paused)
        {
            if (paused == _paused) return;
            if (paused)
            {
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _savedTimeScale;
            }
            _paused = paused;
            _ticks.Paused = paused;
        }

        private void OnDestroy()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions = Array.Empty<IDisposable>();
            _pauseInput?.Dispose();
            _pauseInput = null;
            // Time.timeScale outlives the scene: never hand the next one a frozen clock.
            if (_paused) Time.timeScale = _savedTimeScale;
        }
    }
}
