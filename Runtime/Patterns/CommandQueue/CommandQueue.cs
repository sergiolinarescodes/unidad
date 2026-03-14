using System;
using System.Collections.Generic;

namespace Unidad.Core.Patterns.CommandQueue
{
    /// <summary>
    /// Sequential command execution queue with pause/resume support.
    /// Per-entity pattern — create one per entity that needs queued actions.
    /// </summary>
    public sealed class CommandQueue
    {
        private readonly LinkedList<ICommand> _queue = new();

        public bool IsPaused { get; private set; }
        public bool IsEmpty => _queue.Count == 0 && Current == null;
        public ICommand Current { get; private set; }
        public int Count => _queue.Count + (Current != null ? 1 : 0);

        public event Action<ICommand> OnCommandCompleted;
        public event Action<ICommand> OnCommandFailed;
        public event Action OnQueueEmpty;

        public void Enqueue(ICommand command)
        {
            _queue.AddLast(command);
        }

        public void InsertFront(ICommand command)
        {
            _queue.AddFirst(command);
        }

        public void EnqueueRange(IEnumerable<ICommand> commands)
        {
            foreach (var command in commands)
                _queue.AddLast(command);
        }

        public void Clear()
        {
            Current?.Cancel();
            Current = null;
            while (_queue.Count > 0)
            {
                _queue.First.Value.Cancel();
                _queue.RemoveFirst();
            }
        }

        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;

        public void Tick(ICommandContext context, float deltaTime)
        {
            if (IsPaused) return;

            if (Current == null)
            {
                if (_queue.Count == 0) return;
                Current = _queue.First.Value;
                _queue.RemoveFirst();
            }

            var status = Current.Execute(context, deltaTime);

            switch (status)
            {
                case CommandStatus.Completed:
                    var completed = Current;
                    Current = null;
                    OnCommandCompleted?.Invoke(completed);
                    if (_queue.Count == 0)
                        OnQueueEmpty?.Invoke();
                    break;

                case CommandStatus.Failed:
                    var failed = Current;
                    Current = null;
                    OnCommandFailed?.Invoke(failed);
                    if (_queue.Count == 0)
                        OnQueueEmpty?.Invoke();
                    break;

                case CommandStatus.Running:
                    break;
            }
        }
    }
}
