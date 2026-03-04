using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.WorldSpace
{
    /// <summary>
    /// Handle given to custom animators. Call <see cref="Release"/> when the animation is finished
    /// to return the instance to the pool.
    /// </summary>
    public readonly struct FloatingTextHandle
    {
        internal readonly WorldFloatingTextService.PooledEntry Entry;
        private readonly WorldFloatingTextService _service;

        internal FloatingTextHandle(WorldFloatingTextService.PooledEntry entry, WorldFloatingTextService service)
        {
            Entry = entry;
            _service = service;
        }

        public Transform Transform => Entry.Go.transform;
        public Label Label => Entry.Label;
        public bool IsValid => Entry?.Go != null;

        /// <summary>Return this instance to the pool. Must be called exactly once.</summary>
        public void Release()
        {
            if (_service != null)
                _service.ReturnToPool(this);
        }
    }

    /// <summary>
    /// Custom animation callback. Receives a fully configured handle (positioned, label set, billboard applied).
    /// The implementor MUST call <see cref="FloatingTextHandle.Release"/> when done.
    /// </summary>
    public delegate void FloatingTextAnimator(FloatingTextHandle handle);
}
