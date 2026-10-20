using System;
using System.Collections.Generic;
using System.Linq;
using Unidad.Core.Diagnostics;
using UnityEngine;
using UnityEngine.LowLevel;

namespace Unidad.Adapter
{
    /// <summary>Puts a TickRunner's four systems into the PlayerLoop (<see cref="PlayerLoopPlan"/>) and strips them by marker name.</summary>
    internal static class PlayerLoopInstaller
    {
        public static void Insert(TickRunner runner)
        {
            var root = PlayerLoop.GetCurrentPlayerLoop();
            var phases = Strip(root.subSystemList ?? Array.Empty<PlayerLoopSystem>());
            var placed = new[]
            {
                Add(phases, PlayerLoopPlan.Input, typeof(UnidadTick.Input), runner.StepInput),
                Add(phases, PlayerLoopPlan.Update, typeof(UnidadTick.Update), runner.StepUpdate),
                Add(phases, PlayerLoopPlan.LateUpdate, typeof(UnidadTick.LateUpdate), runner.StepLate),
                Add(phases, PlayerLoopPlan.Fixed, typeof(UnidadTick.Fixed), runner.StepFixed),
            };
            root.subSystemList = phases;
            PlayerLoop.SetPlayerLoop(root);
            Log.Info("[TickRunner] PlayerLoop: " + string.Join(", ", placed));
        }

        /// <summary>Removes every UnidadTick system, whichever code load its marker type came from.</summary>
        public static void Remove()
        {
            var root = PlayerLoop.GetCurrentPlayerLoop();
            if (root.subSystemList == null) return;
            var before = Count(root.subSystemList);
            var phases = Strip(root.subSystemList);
            if (Count(phases) == before) return;
            root.subSystemList = phases;
            PlayerLoop.SetPlayerLoop(root);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void StripOnLoad() => Remove();

        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        private static void StripOnUnload() => Remove();

        private static PlayerLoopSystem[] Strip(PlayerLoopSystem[] phases)
        {
            var result = (PlayerLoopSystem[])phases.Clone();
            for (var i = 0; i < result.Length; i++)
            {
                var children = result[i].subSystemList;
                if (children == null) continue;
                var kept = PlayerLoopPlan.Strip(children, system => system.type?.FullName);
                if (kept != null) result[i].subSystemList = kept.ToArray();
            }
            return result;
        }

        /// <summary>Returns where the marker went, as <c>Name@Phase[index]</c>.</summary>
        private static string Add(PlayerLoopSystem[] phases, PlayerLoopPlan.Slot slot, Type marker, Action step)
        {
            var phase = PlayerLoopPlan.PhaseIndex(phases.Select(Name).ToList(), slot.Phase);
            var children = new List<PlayerLoopSystem>(phases[phase].subSystemList ?? Array.Empty<PlayerLoopSystem>());
            var at = PlayerLoopPlan.InsertIndex(children.Select(Name).ToList(), slot, Log.Warning);
            children.Insert(at, new PlayerLoopSystem { type = marker, updateDelegate = () => step() });
            phases[phase].subSystemList = children.ToArray();
            return $"{marker.Name}@{slot.Phase}[{at}]";
        }

        private static string Name(PlayerLoopSystem system) => system.type?.Name;

        private static int Count(PlayerLoopSystem[] phases) => phases.Sum(phase => phase.subSystemList?.Length ?? 0);
    }
}
