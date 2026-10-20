using Unidad.Core.Diagnostics;
using Unidad.Core.EventBus;
using Unidad.Core.Patterns.StateMachine;
using Unidad.Core.Systems;

namespace Unidad.Flow
{
    /// <summary>
    /// The flow's state machine. Every transition publishes <see cref="FlowStateChangedEvent"/>; the adapter mirrors it
    /// (curtain, pause menu, pause, quit) and decides nothing.
    /// </summary>
    internal sealed class FlowService : SystemServiceBase, IFlowService
    {
        private readonly StateMachine<FlowService> _machine;
        private FlowStep _current;

        public FlowService(IEventBus bus) : base(bus)
        {
            _machine = new StateMachine<FlowService>(this);
            _current = Steps.Boot;
            _machine.TransitionTo(_current);
        }

        internal FlowSteps Steps { get; } = new();

        internal IWorldReadiness Readiness { get; private set; } = ImmediateReadiness.Instance;

        public FlowState State => _current.Id;

        public bool AcceptsGameplayInput => State == FlowState.Play;

        public void Start(IWorldReadiness readiness)
        {
            if (State != FlowState.Boot) return;
            Readiness = readiness ?? ImmediateReadiness.Instance;
            Handle(FlowCommand.Start);
        }

        public void Resume() => Handle(FlowCommand.Resume);

        public void Back() => Handle(FlowCommand.Back);

        public void RequestQuit() => Handle(FlowCommand.RequestQuit);

        public void CancelQuit() => Handle(FlowCommand.CancelQuit);

        public void ConfirmQuit() => Handle(FlowCommand.ConfirmQuit);

        public void Tick(float deltaTime) => _machine.Tick(deltaTime);

        internal void Go(FlowStep next)
        {
            var from = State;
            _current = next;
            _machine.TransitionTo(next);
            Log.Info($"[Flow] {from} -> {next.Id}");
            Publish(new FlowStateChangedEvent(from, next.Id));
        }

        private void Handle(FlowCommand command)
        {
            var next = _current.On(command, this);
            if (next != null) Go(next);
        }
    }
}
