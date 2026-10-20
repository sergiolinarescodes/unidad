using Unidad.Core.Diagnostics;
using Unidad.Core.Patterns.StateMachine;

namespace Unidad.Flow
{
    /// <summary>What a flow verb asks for; each step decides whether it accepts it.</summary>
    internal enum FlowCommand
    {
        Start,
        Resume,
        Back,
        RequestQuit,
        CancelQuit,
        ConfirmQuit,
    }

    /// <summary>One state of the flow: its id, what it does per tick, and which commands lead where.</summary>
    internal abstract class FlowStep : IState<FlowService>
    {
        public abstract FlowState Id { get; }

        public virtual void Enter(FlowService flow) { }

        public virtual void Tick(FlowService flow, float deltaTime) { }

        public virtual void Exit(FlowService flow) { }

        /// <summary>The step <paramref name="command"/> leads to from here, or null when this step refuses it.</summary>
        public virtual FlowStep On(FlowCommand command, FlowService flow) => null;
    }

    internal sealed class BootStep : FlowStep
    {
        public override FlowState Id => FlowState.Boot;

        public override FlowStep On(FlowCommand command, FlowService flow) => command == FlowCommand.Start ? flow.Steps.Loading : null;
    }

    internal sealed class LoadingStep : FlowStep
    {
        private float _waited;

        public override FlowState Id => FlowState.Loading;

        public override void Enter(FlowService flow) => _waited = 0;

        public override void Tick(FlowService flow, float deltaTime)
        {
            _waited += deltaTime;
            if (flow.Readiness.IsReady)
            {
                flow.Go(flow.Steps.Play);
                return;
            }
            if (_waited < FlowTuning.LoadingTimeoutSeconds || !flow.Readiness.CanTimeout) return;
            Log.Warning($"[Flow] The world did not report ready within {FlowTuning.LoadingTimeoutSeconds} s; lifting the curtain anyway.");
            flow.Go(flow.Steps.Play);
        }
    }

    internal sealed class PlayStep : FlowStep
    {
        public override FlowState Id => FlowState.Play;

        public override FlowStep On(FlowCommand command, FlowService flow) => command == FlowCommand.Back ? flow.Steps.Paused : null;
    }

    internal sealed class PausedStep : FlowStep
    {
        public override FlowState Id => FlowState.Paused;

        public override FlowStep On(FlowCommand command, FlowService flow) => command switch
        {
            FlowCommand.Resume or FlowCommand.Back => flow.Steps.Play,
            FlowCommand.RequestQuit => flow.Steps.QuitConfirm,
            _ => null,
        };
    }

    internal sealed class QuitConfirmStep : FlowStep
    {
        public override FlowState Id => FlowState.QuitConfirm;

        public override FlowStep On(FlowCommand command, FlowService flow) => command switch
        {
            FlowCommand.CancelQuit or FlowCommand.Back => flow.Steps.Paused,
            FlowCommand.ConfirmQuit => flow.Steps.Quit,
            _ => null,
        };
    }

    /// <summary>Terminal: nothing leads out of Quit.</summary>
    internal sealed class QuitStep : FlowStep
    {
        public override FlowState Id => FlowState.Quit;
    }

    /// <summary>The one instance of each step a flow owns.</summary>
    internal sealed class FlowSteps
    {
        public BootStep Boot { get; } = new();
        public LoadingStep Loading { get; } = new();
        public PlayStep Play { get; } = new();
        public PausedStep Paused { get; } = new();
        public QuitConfirmStep QuitConfirm { get; } = new();
        public QuitStep Quit { get; } = new();
    }
}
