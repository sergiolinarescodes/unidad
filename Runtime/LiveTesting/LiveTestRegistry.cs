using System;
using UnityEngine;

namespace Unidad.Core.LiveTesting
{
    /// <summary>
    /// The bridge between the runtime DI world and the Editor MCP tools / button panel.
    /// A per-scene bootstrap registers the active <see cref="ILiveTestScene"/> here at
    /// Play start; Editor code reads it WITHOUT needing to know any game-specific
    /// service interface type. Static state, so it is reset by Unity's domain reload —
    /// and explicitly reset before every Play session for safety.
    /// </summary>
    public static class LiveTestRegistry
    {
        /// <summary>The live test for the currently-running scene, or null.</summary>
        public static ILiveTestScene Active { get; private set; }

        /// <summary>
        /// Advances the running scene by ONE deterministic fixed step. The bootstrap
        /// wires this to <c>dt =&gt; { tickRunner.FixedTickAll(dt); Physics2D.Simulate(dt); }</c>
        /// (FixedTick BEFORE Simulate). Null when no live test is active.
        /// </summary>
        public static Action<float> FixedStep { get; private set; }

        public static event Action ActiveChanged;

        public static void SetActive(ILiveTestScene scene, Action<float> fixedStep)
        {
            Active = scene;
            FixedStep = fixedStep;
            ActiveChanged?.Invoke();
        }

        public static void Clear()
        {
            Active = null;
            FixedStep = null;
            ActiveChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetOnPlay() => Clear();
    }
}
