using System;
using Unidad.Adapter.Stage;
using Unidad.Core.Abstractions;
using Unidad.Core.DI;
using Unidad.Core.EventBus;
using Unidad.Core.Ticking;
using Unidad.Flow;
using Unidad.Launch;
using UnityEngine;

namespace Unidad.Adapter
{
    /// <summary>The <c>frames</c> key: counts frames from the curtain lift, publishes the elapsed event, then quits.</summary>
    internal sealed class PlayFrames : MonoBehaviour, ITickable
    {
        private IEventBus _bus;
        private IAppQuit _quit;
        private ITickRegistry _ticks;
        private IDisposable _subscription;
        private int _limit, _count;
        private bool _running, _done;

        /// <summary>Null when the launch line has no <c>frames</c>.</summary>
        public static PlayFrames Bind(Container container, IGameObjectFactory factory, ITickRegistry ticks, LaunchArgs launch)
        {
            if (!launch.TryGetInt(LaunchSettings.Frames, out var limit)) return null;
            if (limit <= 0) throw new InvalidOperationException($"frames={limit}: it must be a positive frame count.");
            var frames = factory.Create<PlayFrames>("[PlayFrames]");
            frames._bus = container.Resolve<IEventBus>();
            frames._quit = container.Resolve<IAppQuit>();
            frames._ticks = ticks;
            frames._limit = limit;
            frames._subscription = frames._bus.Subscribe<FlowStateChangedEvent>(change => frames._running |= change.To == FlowState.Play);
            // Counts while paused too, so a paused run still quits.
            ticks.Register(frames, TickGroup.Presentation, runWhilePaused: true);
            return frames;
        }

        public void Tick(float deltaTime)
        {
            if (!_running || _done || ++_count < _limit) return;
            _done = true;
            _bus.Publish(new PlayFramesElapsedEvent(_count));
            _quit.Quit();
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            _ticks?.Unregister(this);
        }
    }
}
