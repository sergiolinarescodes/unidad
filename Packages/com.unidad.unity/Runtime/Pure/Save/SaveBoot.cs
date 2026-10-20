using System;
using Unidad.Save;
using Unidad.Text;

namespace Unidad.Adapter
{
    /// <summary>
    /// Bound by a game whose title card decides between Continue and New Game: SaveHost then leaves
    /// <see cref="ISaveService.Load"/> / <see cref="ISaveService.StartFresh"/> to the game (<c>fresh=true</c> still starts fresh).
    /// </summary>
    public sealed class SaveLoadDeferred
    {
    }

    internal enum SaveBootStep
    {
        Load,
        StartFresh,
        Wait,
    }

    internal static class SaveBoot
    {
        public static SaveBootStep Decide(bool fresh, bool deferred)
            => fresh ? SaveBootStep.StartFresh : deferred ? SaveBootStep.Wait : SaveBootStep.Load;
    }

    internal static class SaveNotice
    {
        /// <summary>The corner line for a load that left the slot untouched, or null.</summary>
        public static string Line(SaveLoadResult result, ITextService text) => result.Outcome switch
        {
            SaveLoadOutcome.RefusedFutureVersion => text.Format(TextKeys.SaveRefused, result.Version),
            SaveLoadOutcome.Unreadable => text.Get(TextKeys.SaveUnreadable),
            _ => null,
        };
    }

    /// <summary>A handler on a static engine event, attached and detached at most once each (a leaked one pins old code).</summary>
    internal sealed class StaticHook<T> where T : Delegate
    {
        private readonly Action<T> _add, _remove;
        private readonly T _handler;

        public StaticHook(Action<T> add, Action<T> remove, T handler)
        {
            _add = add ?? throw new ArgumentNullException(nameof(add));
            _remove = remove ?? throw new ArgumentNullException(nameof(remove));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public bool Attached { get; private set; }

        public void Attach()
        {
            if (Attached) return;
            _add(_handler);
            Attached = true;
        }

        public void Detach()
        {
            if (!Attached) return;
            _remove(_handler);
            Attached = false;
        }
    }
}
