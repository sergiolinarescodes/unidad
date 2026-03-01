namespace Unidad.Core.Patterns.StateMachine
{
    /// <summary>
    /// Generic state machine state interface.
    /// </summary>
    /// <typeparam name="TContext">The context shared between states (e.g., the entity or system being managed).</typeparam>
    public interface IState<TContext>
    {
        /// <summary>Called when entering this state.</summary>
        void Enter(TContext context);

        /// <summary>Called each tick while in this state.</summary>
        void Tick(TContext context, float deltaTime);

        /// <summary>Called when exiting this state.</summary>
        void Exit(TContext context);
    }

    /// <summary>
    /// Simple state machine that manages state transitions.
    /// </summary>
    public sealed class StateMachine<TContext>
    {
        private IState<TContext> _currentState;
        private readonly TContext _context;

        public IState<TContext> CurrentState => _currentState;

        public StateMachine(TContext context)
        {
            _context = context;
        }

        /// <summary>Transition to a new state.</summary>
        public void TransitionTo(IState<TContext> newState)
        {
            _currentState?.Exit(_context);
            _currentState = newState;
            _currentState?.Enter(_context);
        }

        /// <summary>Tick the current state.</summary>
        public void Tick(float deltaTime)
        {
            _currentState?.Tick(_context, deltaTime);
        }
    }
}
