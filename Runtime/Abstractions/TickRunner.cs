using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// MonoBehaviour that drives all ITickable systems.
    /// Spawned by UnidadBootstrap. Calls Tick(deltaTime) each frame.
    /// In tests, call TickAll(deltaTime) directly without needing Update().
    /// </summary>
    public sealed class TickRunner : MonoBehaviour
    {
        private readonly List<ITickable> _tickables = new();
        private ITimeProvider _timeProvider;

        public void Initialize(ITimeProvider timeProvider, IEnumerable<ITickable> tickables)
        {
            _timeProvider = timeProvider;
            _tickables.AddRange(tickables);
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

        private void Update()
        {
            TickAll(_timeProvider.DeltaTime);
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
    }
}
