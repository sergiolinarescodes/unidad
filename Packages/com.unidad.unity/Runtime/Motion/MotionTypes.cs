using System;
using Unidad.Core.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Adapter.Motion
{
    /// <summary>
    /// One animatable property of a <typeparamref name="T"/>, read and written as up to four floats. Targets are passed to
    /// <see cref="IMotionRunner.Play{T}"/>, never registered; a game View assembly writes its own (TMP colour, scale).
    /// </summary>
    public interface IMotionTarget<in T> where T : class
    {
        /// <summary>How many of the four floats are used (1..4).</summary>
        int Width { get; }

        Vector4 Read(T target);

        void Write(T target, Vector4 value);
    }

    /// <summary>The framework's targets: stateless singletons, plus factories for MaterialPropertyBlock properties.</summary>
    public static class MotionTargets
    {
        /// <summary>World position.</summary>
        public static IMotionTarget<Transform> Position => throw new NotImplementedException();

        public static IMotionTarget<Transform> LocalPosition => throw new NotImplementedException();

        /// <summary>Quaternion xyzw; the write normalises (nlerp).</summary>
        public static IMotionTarget<Transform> LocalRotation => throw new NotImplementedException();

        public static IMotionTarget<Transform> LocalScale => throw new NotImplementedException();

        /// <summary>style.opacity.</summary>
        public static IMotionTarget<VisualElement> Opacity => throw new NotImplementedException();

        /// <summary>style.translate in pixels (x, y).</summary>
        public static IMotionTarget<VisualElement> Translate => throw new NotImplementedException();

        /// <summary>style.scale (x, y).</summary>
        public static IMotionTarget<VisualElement> UiScale => throw new NotImplementedException();

        public static IMotionTarget<Camera> FieldOfView => throw new NotImplementedException();

        /// <summary>A new instance holding its own scratch MaterialPropertyBlock.</summary>
        public static IMotionTarget<Renderer> MpbFloat(int propertyId) => throw new NotImplementedException();

        public static IMotionTarget<Renderer> MpbColor(int propertyId) => throw new NotImplementedException();
    }

    public enum MotionCurveKind : byte
    {
        Tween = 0,
        Arc = 1,
        Punch = 2,
        Shake = 3,
    }

    /// <summary>The path a motion takes between its start and end values.</summary>
    public readonly record struct MotionCurve(MotionCurveKind Kind, float Height = 0f, PunchSpec Punch = default, ShakeSpec Shake = default, int Axis = 1)
    {
        public static MotionCurve Tween => default;

        public static MotionCurve ArcOf(float height) => throw new NotImplementedException();

        public static MotionCurve PunchOf(PunchSpec punch) => throw new NotImplementedException();

        public static MotionCurve ShakeOf(ShakeSpec shake, int axis) => throw new NotImplementedException();
    }

    /// <summary>The channels a motion runs on: a new motion on the same (owner, channel) cancels the running one.</summary>
    public static class MotionChannel
    {
        public const string Move = "move", Rotate = "rotate", Scale = "scale", Opacity = "opacity",
            Translate = "translate", Color = "color", Flash = "flash", Fov = "fov";
    }

    public readonly record struct MotionHandle(int Slot, int Generation)
    {
        public static MotionHandle None => default;
    }

    /// <summary>Runs every motion of the scene from the Presentation tick (also while paused, on unscaled time).</summary>
    public interface IMotionRunner
    {
        /// <summary>anim.skip: every Play completes at once.</summary>
        bool SkipAll { get; set; }

        int Active { get; }

        MotionHandle Play<T>(T target, IMotionTarget<T> adapter, Vector4 to, MotionSpec spec, string channel,
            MotionCurve curve = default, bool runWhilePaused = false, Action onComplete = null) where T : class;

        bool IsPlaying(MotionHandle handle);

        bool IsPlaying(object owner, string channel);

        /// <summary>The value stays where it is; onComplete is NOT called.</summary>
        void Cancel(object owner, string channel);

        /// <summary>Writes the end value; onComplete is called.</summary>
        void Complete(object owner, string channel);

        void CancelAll(object owner);
    }

    /// <summary>Thin sugar over <see cref="IMotionRunner.Play{T}"/>; the owner is the target.</summary>
    public static class MotionRunnerExtensions
    {
        public static MotionHandle Move(this IMotionRunner runner, Transform target, Vector3 to, MotionSpec spec, bool local = false, string channel = MotionChannel.Move)
            => throw new NotImplementedException();

        public static MotionHandle Arc(this IMotionRunner runner, Transform target, Vector3 to, float height, MotionSpec spec, string channel = MotionChannel.Move)
            => throw new NotImplementedException();

        public static MotionHandle Rotate(this IMotionRunner runner, Transform target, Quaternion to, MotionSpec spec, string channel = MotionChannel.Rotate)
            => throw new NotImplementedException();

        public static MotionHandle Scale(this IMotionRunner runner, Transform target, Vector3 to, MotionSpec spec, string channel = MotionChannel.Scale)
            => throw new NotImplementedException();

        public static MotionHandle Punch(this IMotionRunner runner, Transform target, PunchSpec punch, string channel = MotionChannel.Scale)
            => throw new NotImplementedException();

        /// <summary>A shake of the euler <paramref name="axis"/>, in degrees.</summary>
        public static MotionHandle Shake(this IMotionRunner runner, Transform target, ShakeSpec shake, int axis = 2, string channel = MotionChannel.Rotate)
            => throw new NotImplementedException();

        public static MotionHandle Fade(this IMotionRunner runner, VisualElement element, float to, MotionSpec spec, bool runWhilePaused = false)
            => throw new NotImplementedException();

        public static MotionHandle Translate(this IMotionRunner runner, VisualElement element, Vector2 toPixels, MotionSpec spec)
            => throw new NotImplementedException();

        public static MotionHandle Fov(this IMotionRunner runner, Camera camera, float to, MotionSpec spec)
            => throw new NotImplementedException();

        public static MotionHandle MpbFloat(this IMotionRunner runner, Renderer renderer, int propertyId, float to, MotionSpec spec, string channel)
            => throw new NotImplementedException();

        public static MotionHandle MpbColor(this IMotionRunner runner, Renderer renderer, int propertyId, Color to, MotionSpec spec, string channel = MotionChannel.Color)
            => throw new NotImplementedException();
    }
}
