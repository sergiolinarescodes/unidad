using System;
using System.Collections.Concurrent;
using Unidad.Core.Abstractions;
using Unidad.Core.Diagnostics;

namespace Unidad.Core.Jobs
{
    /// <summary>
    /// The main-thread end of <see cref="IMainThreadDispatcher"/>: a tickable that runs at most
    /// <see cref="ActionsPerTick"/> posted actions per tick. An action posted while it drains waits
    /// for the next tick, so a chain of posts cannot pin one frame.
    /// </summary>
    public sealed class MainThreadDispatcher : IMainThreadDispatcher, ITickable
    {
        private readonly ConcurrentQueue<Action> _queue = new();

        public MainThreadDispatcher(int actionsPerTick)
        {
            if (actionsPerTick < 1)
                throw new ArgumentOutOfRangeException(nameof(actionsPerTick), actionsPerTick, "At least one action per tick.");
            ActionsPerTick = actionsPerTick;
        }

        public int ActionsPerTick { get; }

        public int Pending => _queue.Count;

        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _queue.Enqueue(action);
        }

        public void Tick(float deltaTime)
        {
            var budget = Math.Min(ActionsPerTick, _queue.Count);
            for (var i = 0; i < budget && _queue.TryDequeue(out var action); i++)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error($"[MainThreadDispatcher] a posted action threw; the rest still run. {ex.GetType().Name}: {ex.Message}");
                    Log.Exception(ex);
                }
            }
        }
    }
}
