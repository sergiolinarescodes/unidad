using System;
using System.Collections.Generic;

namespace Unidad.Adapter
{
    /// <summary>Where the four UnidadTick systems go in the PlayerLoop, over system type names only (engine-free, smoke-tested).</summary>
    internal static class PlayerLoopPlan
    {
        /// <summary>The marker class; its nested types' FullNames start with it, and FullName survives a code reload.</summary>
        public const string MarkerPrefix = "Unidad.Adapter.UnidadTick";

        /// <summary>One insertion: the phase, the anchor system and whether ours goes after (true) or before it.</summary>
        public readonly record struct Slot(string Phase, string Anchor, bool After);

        public static Slot Input => new("PreUpdate", "NewInputUpdate", true);
        public static Slot Update => new("Update", "ScriptRunBehaviourUpdate", false);
        public static Slot LateUpdate => new("PreLateUpdate", "ScriptRunBehaviourLateUpdate", false);
        public static Slot Fixed => new("FixedUpdate", "ScriptRunBehaviourFixedUpdate", false);

        /// <summary>The phase's index among the loop's top-level systems; a missing phase throws.</summary>
        public static int PhaseIndex(IReadOnlyList<string> phases, string phase)
        {
            for (var i = 0; i < phases.Count; i++)
                if (phases[i] == phase) return i;
            throw new InvalidOperationException($"PlayerLoop has no {phase} phase.");
        }

        /// <summary>The index to insert at inside the phase; a missing anchor appends and warns.</summary>
        public static int InsertIndex(IReadOnlyList<string> systems, Slot slot, Action<string> warn)
        {
            for (var i = 0; i < systems.Count; i++)
                if (systems[i] == slot.Anchor) return slot.After ? i + 1 : i;
            warn?.Invoke($"[TickRunner] {slot.Anchor} not found in {slot.Phase}; appended.");
            return systems.Count;
        }

        public static bool IsMarker(string fullName) =>
            fullName != null && (fullName == MarkerPrefix || fullName.StartsWith(MarkerPrefix + "+", StringComparison.Ordinal));

        /// <summary>The systems without ours; null when nothing was removed, so the caller can skip the rewrite.</summary>
        public static List<T> Strip<T>(IReadOnlyList<T> systems, Func<T, string> fullName)
        {
            List<T> kept = null;
            for (var i = 0; i < systems.Count; i++)
            {
                if (!IsMarker(fullName(systems[i]))) { kept?.Add(systems[i]); continue; }
                if (kept != null) continue;
                kept = new List<T>(systems.Count);
                for (var j = 0; j < i; j++) kept.Add(systems[j]);
            }
            return kept;
        }
    }
}
