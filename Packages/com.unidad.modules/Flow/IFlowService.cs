using Unidad.Core.Abstractions;

namespace Unidad.Flow
{
    /// <summary>
    /// Boot, the loading curtain, play, pause and the quit question. Every transition logs and publishes
    /// <see cref="FlowStateChangedEvent"/>; a verb the current state refuses does nothing.
    /// </summary>
    public interface IFlowService : ITickable
    {
        FlowState State { get; }

        /// <summary>True in <see cref="FlowState.Play"/>.</summary>
        bool AcceptsGameplayInput { get; }

        /// <summary>Only from Boot; null means <see cref="ImmediateReadiness"/>.</summary>
        void Start(IWorldReadiness readiness);

        void Resume();
        void Back();
        void RequestQuit();
        void CancelQuit();
        void ConfirmQuit();
    }
}
