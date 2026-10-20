using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Systems;

namespace Unidad.Save
{
    /// <summary>
    /// The autosave triggers: events save on the next tick, quit events save at once, and every
    /// <see cref="AutosaveTuning.PeriodSeconds"/> of tick time while a settled predicate holds.
    /// </summary>
    internal sealed class AutosaveService : SystemServiceBase, IAutosave
    {
        private readonly ISaveService _save;
        private readonly AutosaveTuning _tuning;
        private readonly List<Func<bool>> _settled = new();
        private bool _requested;

        public AutosaveService(IEventBus eventBus, ISaveService save, AutosaveTuning tuning) : base(eventBus)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Subscribe<GameSavedEvent>(_ => SettledSeconds = 0);
        }

        public double SettledSeconds { get; private set; }

        public IDisposable On<TEvent>(Func<TEvent, bool> when = null) where TEvent : struct =>
            Track(EventBus.Subscribe<TEvent>(e =>
            {
                if (when == null || when(e)) _requested = true;
            }));

        public IDisposable QuitOn<TEvent>(Func<TEvent, bool> when = null) where TEvent : struct =>
            Track(EventBus.Subscribe<TEvent>(e =>
            {
                if (when == null || when(e)) Quit();
            }));

        public IDisposable PeriodicWhile(Func<bool> settled)
        {
            if (settled == null) throw new ArgumentNullException(nameof(settled));
            _settled.Add(settled);
            return new Release(() => _settled.Remove(settled));
        }

        public bool Quit() => _save.Save();

        public void Tick(float deltaTime)
        {
            if (_requested)
            {
                _requested = false;
                _save.Save();
                return;
            }
            if (_tuning.PeriodSeconds <= 0 || !Settled()) return;
            SettledSeconds += Math.Max(0, deltaTime);
            if (SettledSeconds >= _tuning.PeriodSeconds && _save.Save()) SettledSeconds = 0;
        }

        private bool Settled()
        {
            foreach (var settled in _settled)
                if (settled()) return true;
            return false;
        }

        private IDisposable Track(IDisposable subscription)
        {
            Subscriptions.Add(subscription);
            return subscription;
        }
    }
}
