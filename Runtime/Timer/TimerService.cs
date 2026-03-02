using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;

namespace Unidad.Core.Timer
{
    internal sealed class TimerService : SystemServiceBase, ITimerService, ITickable
    {
        private int _nextId = 1;
        private readonly Dictionary<int, TimerEntry> _timers = new();
        private readonly List<int> _completedBuffer = new();

        public TimerService(IEventBus eventBus) : base(eventBus) { }

        public TimerHandle Start(float duration, Action onComplete = null, bool loop = false)
        {
            if (duration <= 0f)
                throw new ArgumentException("Duration must be positive.", nameof(duration));

            var handle = new TimerHandle(_nextId++);
            _timers[handle.Id] = new TimerEntry(duration, onComplete, loop);
            return handle;
        }

        public void Cancel(TimerHandle handle)
        {
            if (!_timers.Remove(handle.Id)) return;
            Publish(new TimerCancelledEvent(handle));
        }

        public void Pause(TimerHandle handle)
        {
            if (_timers.TryGetValue(handle.Id, out var entry))
                entry.Paused = true;
        }

        public void Resume(TimerHandle handle)
        {
            if (_timers.TryGetValue(handle.Id, out var entry))
                entry.Paused = false;
        }

        public float GetRemaining(TimerHandle handle)
        {
            return _timers.TryGetValue(handle.Id, out var entry)
                ? entry.Duration - entry.Elapsed
                : 0f;
        }

        public float GetProgress(TimerHandle handle)
        {
            if (!_timers.TryGetValue(handle.Id, out var entry)) return 1f;
            return entry.Duration > 0f
                ? Math.Min(entry.Elapsed / entry.Duration, 1f)
                : 1f;
        }

        public bool IsRunning(TimerHandle handle)
        {
            return _timers.TryGetValue(handle.Id, out var entry) && !entry.Paused;
        }

        public void Tick(float deltaTime)
        {
            _completedBuffer.Clear();

            foreach (var kvp in _timers)
            {
                var entry = kvp.Value;
                if (entry.Paused) continue;

                entry.Elapsed += deltaTime;

                if (entry.Elapsed >= entry.Duration)
                {
                    entry.OnComplete?.Invoke();
                    if (entry.Loop)
                    {
                        entry.Elapsed -= entry.Duration;
                    }
                    else
                    {
                        _completedBuffer.Add(kvp.Key);
                    }
                    Publish(new TimerCompletedEvent(new TimerHandle(kvp.Key)));
                }
            }

            foreach (var id in _completedBuffer)
                _timers.Remove(id);
        }

        private sealed class TimerEntry
        {
            public readonly float Duration;
            public readonly Action OnComplete;
            public readonly bool Loop;
            public float Elapsed;
            public bool Paused;

            public TimerEntry(float duration, Action onComplete, bool loop)
            {
                Duration = duration;
                OnComplete = onComplete;
                Loop = loop;
            }
        }
    }
}
