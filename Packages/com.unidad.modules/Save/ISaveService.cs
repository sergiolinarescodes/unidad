using System;
using Unidad.Core.Abstractions;

namespace Unidad.Save
{
    /// <summary>
    /// One versioned slot assembled from participants. A refused or unreadable slot is never overwritten; a load applies every
    /// section or none.
    /// </summary>
    public interface ISaveService : ITickable
    {
        /// <summary>Null throws ArgumentNullException; a blank or duplicate key throws ArgumentException.</summary>
        void Register(ISaveParticipant participant);

        SaveLoadResult Load();

        /// <summary>Starts without reading the slot (NoSave "fresh start"); writing is allowed and publishes <see cref="GameLoadedEvent"/>.</summary>
        SaveLoadResult StartFresh();

        /// <summary>The store has a slot (an unreadable slot counts as present).</summary>
        bool HasSave { get; }

        bool CanWrite { get; }

        bool Save();

        /// <summary>Defers saves while <paramref name="busy"/> is true; the deferred save runs on the first tick after.</summary>
        IDisposable HoldWhile(Func<bool> busy);

        bool Pending { get; }

        /// <summary>Saves inside the publish of every matching <typeparamref name="TEvent"/>.</summary>
        IDisposable AutosaveOn<TEvent>(Func<TEvent, bool> when = null) where TEvent : struct;

        /// <summary>Saves on the next tick after matching events; several in one frame make one save.</summary>
        IDisposable AutosaveAfter<TEvent>(Func<TEvent, bool> when = null) where TEvent : struct;
    }

    /// <summary><see cref="PeriodSeconds"/> &lt;= 0 disables the periodic save.</summary>
    public sealed record AutosaveTuning
    {
        public double PeriodSeconds { get; init; } = 120;
    }

    /// <summary>When the game saves by itself: on events, at quit and periodically while settled (counted from tick time).</summary>
    public interface IAutosave : ITickable
    {
        /// <summary>Saves on the next tick after a matching event.</summary>
        IDisposable On<TEvent>(Func<TEvent, bool> when = null) where TEvent : struct;

        /// <summary>Saves at once on a matching event.</summary>
        IDisposable QuitOn<TEvent>(Func<TEvent, bool> when = null) where TEvent : struct;

        IDisposable PeriodicWhile(Func<bool> settled);

        /// <summary>Saves now.</summary>
        bool Quit();

        double SettledSeconds { get; }
    }
}
