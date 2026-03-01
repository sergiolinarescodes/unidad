using System;

namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Abstraction over animations/tweens.
    /// Production: plays actual animations with duration.
    /// Tests: resolves instantly so logic tests don't wait.
    /// </summary>
    public interface IAnimationResolver
    {
        /// <summary>
        /// Play an animation and invoke onComplete when done.
        /// In tests, onComplete fires immediately.
        /// </summary>
        void Play(string animationId, Action onComplete = null);

        /// <summary>Whether animations resolve instantly (test mode).</summary>
        bool IsInstant { get; }
    }

    /// <summary>
    /// Test implementation: all animations complete instantly.
    /// Use in Edit Mode tests to skip animation waits.
    /// </summary>
    public sealed class InstantAnimationResolver : IAnimationResolver
    {
        public bool IsInstant => true;

        public void Play(string animationId, Action onComplete = null)
        {
            onComplete?.Invoke();
        }
    }
}
