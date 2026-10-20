using System.Collections.Generic;
using NUnit.Framework;
using Unidad.Core.EventBus;
using Unidad.Flow;

namespace Unidad.Flow.Smoke
{
    public sealed class FlowSmokeTests
    {
        private sealed class Never : IWorldReadiness
        {
            public bool IsReady => false;
        }

        private sealed class EventBus : IEventBus
        {
            private readonly List<(System.Type Type, System.Delegate Handler)> _handlers = new();

            public System.IDisposable Subscribe<T>(System.Action<T> handler) where T : struct
            {
                _handlers.Add((typeof(T), handler));
                return new Gone(() => Unsubscribe(handler));
            }

            public void Publish<T>(T eventData) where T : struct
            {
                foreach (var (type, handler) in _handlers.ToArray())
                    if (type == typeof(T)) ((System.Action<T>)handler)(eventData);
            }

            public void Unsubscribe<T>(System.Action<T> handler) where T : struct => _handlers.RemoveAll(h => h.Handler.Equals(handler));

            public void ClearAllSubscriptions() => _handlers.Clear();

            private sealed class Gone(System.Action action) : System.IDisposable
            {
                public void Dispose() => action();
            }
        }

        [Test]
        public void TransitionTable_RefusedVerbsAreNoOps()
        {
            var bus = new EventBus();
            var seen = new List<(FlowState, FlowState)>();
            bus.Subscribe<FlowStateChangedEvent>(e => seen.Add((e.From, e.To)));
            var flow = new FlowService(bus);
            Assert.That(flow.State, Is.EqualTo(FlowState.Boot));

            flow.Start(null);
            flow.Tick(0.016f);
            Assert.That(flow.AcceptsGameplayInput, Is.True);
            flow.ConfirmQuit();
            Assert.That(flow.State, Is.EqualTo(FlowState.Play));
            flow.Back();
            flow.RequestQuit();
            flow.Back();
            flow.Resume();

            Assert.That(flow.State, Is.EqualTo(FlowState.Play));
            Assert.That(seen, Is.EqualTo(new[]
            {
                (FlowState.Boot, FlowState.Loading), (FlowState.Loading, FlowState.Play), (FlowState.Play, FlowState.Paused),
                (FlowState.Paused, FlowState.QuitConfirm), (FlowState.QuitConfirm, FlowState.Paused), (FlowState.Paused, FlowState.Play),
            }));

            var scenes = new SceneSwitchService(bus, flow);
            Assert.That(scenes.SwitchTo(new SceneId("next")), Is.True);
            Assert.That(scenes.SwitchTo(new SceneId("other")), Is.False);
            Assert.That(scenes.Pending, Is.EqualTo(new SceneId("next")));
        }

        [Test]
        public void LoadingTimesOutOnlyWhenAllowed_FocusGatesBack()
        {
            var flow = new FlowService(new EventBus());
            flow.Start(new Never());
            for (int i = 0; i < 31; i++) flow.Tick(1f);
            Assert.That(flow.State, Is.EqualTo(FlowState.Play));

            var required = new FlowService(new EventBus());
            required.Start(new RequiredReadiness(new Never(), () => false));
            for (int i = 0; i < 31; i++) required.Tick(1f);
            Assert.That(required.State, Is.EqualTo(FlowState.Loading));

            var focus = new InputFocusService(new EventBus());
            Assert.That(focus.RequestFocus(InputFocusLayer.None), Is.False);
            focus.RequestFocus(InputFocusLayer.World);
            Assert.That(FlowInput.BackAllowed(focus.CurrentFocus), Is.True);
            focus.RequestFocus(InputFocusLayer.Modal);
            Assert.That(FlowInput.BackAllowed(focus.CurrentFocus), Is.False);
            Assert.That(focus.IsBlocked(InputFocusLayer.World), Is.True);
            focus.ReleaseFocus(InputFocusLayer.Modal);
            Assert.That(focus.HasFocus(InputFocusLayer.World), Is.True);
        }
    }
}
