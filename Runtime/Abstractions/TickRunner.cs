using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// MonoBehaviour that drives all ITickable and IFixedTickable systems.
    /// Spawned by UnidadBootstrap. Calls Tick(deltaTime) each frame
    /// and FixedTick(fixedDeltaTime) each fixed timestep.
    /// In tests, call TickAll/FixedTickAll directly without needing Unity's loops.
    /// </summary>
    public sealed class TickRunner : MonoBehaviour
    {
        private readonly List<ITickable> _tickables = new();
        private readonly List<IFixedTickable> _fixedTickables = new();
        private ITimeProvider _timeProvider;

        public void Initialize(ITimeProvider timeProvider, IEnumerable<ITickable> tickables)
        {
            _timeProvider = timeProvider;
            _tickables.AddRange(tickables);
        }

        public void Initialize(ITimeProvider timeProvider, IEnumerable<ITickable> tickables, IEnumerable<IFixedTickable> fixedTickables)
        {
            _timeProvider = timeProvider;
            _tickables.AddRange(tickables);
            _fixedTickables.AddRange(fixedTickables);
        }

        public void Register(ITickable tickable)
        {
            if (!_tickables.Contains(tickable))
                _tickables.Add(tickable);
        }

        public void Unregister(ITickable tickable)
        {
            _tickables.Remove(tickable);
        }

        public void RegisterFixed(IFixedTickable fixedTickable)
        {
            if (!_fixedTickables.Contains(fixedTickable))
                _fixedTickables.Add(fixedTickable);
        }

        public void UnregisterFixed(IFixedTickable fixedTickable)
        {
            _fixedTickables.Remove(fixedTickable);
        }

        private void Update()
        {
            TickAll(_timeProvider.DeltaTime);
        }

        private void FixedUpdate()
        {
            FixedTickAll(_timeProvider.FixedDeltaTime);
        }

        /// <summary>
        /// Manually tick all systems. Use in tests to step without Unity's frame loop.
        /// </summary>
        public void TickAll(float deltaTime)
        {
            for (int i = 0; i < _tickables.Count; i++)
            {
                _tickables[i].Tick(deltaTime);
            }
        }

        /// <summary>
        /// Manually fixed-tick all systems. Use in tests to step without Unity's physics loop.
        /// </summary>
        public void FixedTickAll(float fixedDeltaTime)
        {
            for (int i = 0; i < _fixedTickables.Count; i++)
            {
                _fixedTickables[i].FixedTick(fixedDeltaTime);
            }
        }
    }
}
