using System;

namespace Unidad.Core.HistoryService.Data
{
    /// <summary>
    /// Represents a point in time during game execution.
    /// Tick is the game tick number, SubTick orders events within a tick.
    /// </summary>
    public readonly record struct HistoryTimestamp(
        int Tick,
        int SubTick
    ) : IComparable<HistoryTimestamp>
    {
        public int CompareTo(HistoryTimestamp other)
        {
            var tickCompare = Tick.CompareTo(other.Tick);
            return tickCompare != 0 ? tickCompare : SubTick.CompareTo(other.SubTick);
        }

        public static bool operator <(HistoryTimestamp left, HistoryTimestamp right)
            => left.CompareTo(right) < 0;

        public static bool operator >(HistoryTimestamp left, HistoryTimestamp right)
            => left.CompareTo(right) > 0;

        public static bool operator <=(HistoryTimestamp left, HistoryTimestamp right)
            => left.CompareTo(right) <= 0;

        public static bool operator >=(HistoryTimestamp left, HistoryTimestamp right)
            => left.CompareTo(right) >= 0;

        public override string ToString() => $"[{Tick}.{SubTick}]";
    }
}
