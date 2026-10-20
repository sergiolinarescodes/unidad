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
        private static readonly IMotionTarget<Transform> PositionTarget = new TransformTarget(TransformProperty.Position);
        private static readonly IMotionTarget<Transform> LocalPositionTarget = new TransformTarget(TransformProperty.LocalPosition);
        private static readonly IMotionTarget<Transform> LocalRotationTarget = new TransformTarget(TransformProperty.LocalRotation);
        private static readonly IMotionTarget<Transform> LocalScaleTarget = new TransformTarget(TransformProperty.LocalScale);
        private static readonly IMotionTarget<Transform> LocalEulerTarget = new TransformTarget(TransformProperty.LocalEuler);
        private static readonly IMotionTarget<VisualElement> OpacityTarget = new OpacityStyle();
        private static readonly IMotionTarget<VisualElement> TranslateTarget = new TranslateStyle();
        private static readonly IMotionTarget<VisualElement> UiScaleTarget = new ScaleStyle();
        private static readonly IMotionTarget<Camera> FieldOfViewTarget = new CameraFov();

        /// <summary>World position.</summary>
        public static IMotionTarget<Transform> Position => PositionTarget;

        public static IMotionTarget<Transform> LocalPosition => LocalPositionTarget;

        /// <summary>Quaternion xyzw; the write normalises (nlerp).</summary>
        public static IMotionTarget<Transform> LocalRotation => LocalRotationTarget;

        public static IMotionTarget<Transform> LocalScale => LocalScaleTarget;

        /// <summary>style.opacity.</summary>
        public static IMotionTarget<VisualElement> Opacity => OpacityTarget;

        /// <summary>style.translate in pixels (x, y).</summary>
        public static IMotionTarget<VisualElement> Translate => TranslateTarget;

        /// <summary>style.scale (x, y).</summary>
        public static IMotionTarget<VisualElement> UiScale => UiScaleTarget;

        public static IMotionTarget<Camera> FieldOfView => FieldOfViewTarget;

        /// <summary>Local euler angles in degrees (the shake target).</summary>
        internal static IMotionTarget<Transform> LocalEuler => LocalEulerTarget;

        /// <summary>A new instance holding its own scratch MaterialPropertyBlock.</summary>
        public static IMotionTarget<Renderer> MpbFloat(int propertyId) => new MpbTarget(propertyId, color: false);

        public static IMotionTarget<Renderer> MpbColor(int propertyId) => new MpbTarget(propertyId, color: true);

        private enum TransformProperty : byte
        {
            Position,
            LocalPosition,
            LocalRotation,
            LocalScale,
            LocalEuler,
        }

        private sealed class TransformTarget : IMotionTarget<Transform>
        {
            private readonly TransformProperty _property;

            public TransformTarget(TransformProperty property) => _property = property;

            public int Width => _property == TransformProperty.LocalRotation ? 4 : 3;

            public Vector4 Read(Transform target)
            {
                switch (_property)
                {
                    case TransformProperty.Position: return target.position;
                    case TransformProperty.LocalPosition: return target.localPosition;
                    case TransformProperty.LocalScale: return target.localScale;
                    case TransformProperty.LocalEuler: return target.localEulerAngles;
                    default:
                        var q = target.localRotation;
                        return new Vector4(q.x, q.y, q.z, q.w);
                }
            }

            public void Write(Transform target, Vector4 value)
            {
                switch (_property)
                {
                    case TransformProperty.Position: target.position = value; break;
                    case TransformProperty.LocalPosition: target.localPosition = value; break;
                    case TransformProperty.LocalScale: target.localScale = value; break;
                    case TransformProperty.LocalEuler: target.localEulerAngles = value; break;
                    default:
                        var n = value.normalized;
                        target.localRotation = new Quaternion(n.x, n.y, n.z, n.w);
                        break;
                }
            }
        }

        private sealed class OpacityStyle : IMotionTarget<VisualElement>
        {
            public int Width => 1;

            public Vector4 Read(VisualElement target) => new(target.resolvedStyle.opacity, 0f, 0f, 0f);

            public void Write(VisualElement target, Vector4 value) => target.style.opacity = value.x;
        }

        private sealed class TranslateStyle : IMotionTarget<VisualElement>
        {
            public int Width => 2;

            public Vector4 Read(VisualElement target)
            {
                var t = target.resolvedStyle.translate;
                return new Vector4(t.x, t.y, 0f, 0f);
            }

            public void Write(VisualElement target, Vector4 value) =>
                target.style.translate = new Translate(new Length(value.x, LengthUnit.Pixel), new Length(value.y, LengthUnit.Pixel));
        }

        private sealed class ScaleStyle : IMotionTarget<VisualElement>
        {
            public int Width => 2;

            public Vector4 Read(VisualElement target)
            {
                var s = target.resolvedStyle.scale.value;
                return new Vector4(s.x, s.y, 0f, 0f);
            }

            public void Write(VisualElement target, Vector4 value) =>
                target.style.scale = new Scale(new Vector2(value.x, value.y));
        }

        private sealed class CameraFov : IMotionTarget<Camera>
        {
            public int Width => 1;

            public Vector4 Read(Camera target) => new(target.fieldOfView, 0f, 0f, 0f);

            public void Write(Camera target, Vector4 value) => target.fieldOfView = value.x;
        }

        private sealed class MpbTarget : IMotionTarget<Renderer>
        {
            private readonly int _id;
            private readonly bool _color;
            private readonly MaterialPropertyBlock _block = new();

            public MpbTarget(int id, bool color)
            {
                _id = id;
                _color = color;
            }

            public int Width => _color ? 4 : 1;

            public Vector4 Read(Renderer target)
            {
                target.GetPropertyBlock(_block);
                var material = target.sharedMaterial;
                if (_color)
                {
                    if (_block.HasColor(_id)) return _block.GetColor(_id);
                    return material != null && material.HasProperty(_id) ? material.GetColor(_id) : Color.white;
                }

                if (_block.HasFloat(_id)) return new Vector4(_block.GetFloat(_id), 0f, 0f, 0f);
                return new Vector4(material != null && material.HasProperty(_id) ? material.GetFloat(_id) : 0f, 0f, 0f, 0f);
            }

            public void Write(Renderer target, Vector4 value)
            {
                target.GetPropertyBlock(_block);
                if (_color) _block.SetColor(_id, value);
                else _block.SetFloat(_id, value.x);
                target.SetPropertyBlock(_block);
            }
        }
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
            => runner.Play(target, local ? MotionTargets.LocalPosition : MotionTargets.Position, to, spec, channel);

        public static MotionHandle Arc(this IMotionRunner runner, Transform target, Vector3 to, float height, MotionSpec spec, string channel = MotionChannel.Move)
            => runner.Play(target, MotionTargets.Position, to, spec, channel, MotionCurve.ArcOf(height));

        public static MotionHandle Rotate(this IMotionRunner runner, Transform target, Quaternion to, MotionSpec spec, string channel = MotionChannel.Rotate)
        {
            var from = target.localRotation;
            // nlerp takes the short path: flip the end when it lies on the far hemisphere
            var end = Quaternion.Dot(from, to) < 0f ? new Vector4(-to.x, -to.y, -to.z, -to.w) : new Vector4(to.x, to.y, to.z, to.w);
            return runner.Play(target, MotionTargets.LocalRotation, end, spec, channel);
        }

        public static MotionHandle Scale(this IMotionRunner runner, Transform target, Vector3 to, MotionSpec spec, string channel = MotionChannel.Scale)
            => runner.Play(target, MotionTargets.LocalScale, to, spec, channel);

        public static MotionHandle Punch(this IMotionRunner runner, Transform target, PunchSpec punch, string channel = MotionChannel.Scale)
            => runner.Play(target, MotionTargets.LocalScale, target.localScale, new MotionSpec(punch.Duration), channel, MotionCurve.PunchOf(punch));

        /// <summary>A shake of the euler <paramref name="axis"/>, in degrees.</summary>
        public static MotionHandle Shake(this IMotionRunner runner, Transform target, ShakeSpec shake, int axis = 2, string channel = MotionChannel.Rotate)
            => runner.Play(target, MotionTargets.LocalEuler, target.localEulerAngles, new MotionSpec(shake.Duration), channel, MotionCurve.ShakeOf(shake, axis));

        public static MotionHandle Fade(this IMotionRunner runner, VisualElement element, float to, MotionSpec spec, bool runWhilePaused = false)
            => runner.Play(element, MotionTargets.Opacity, new Vector4(to, 0f, 0f, 0f), spec, MotionChannel.Opacity, runWhilePaused: runWhilePaused);

        public static MotionHandle Translate(this IMotionRunner runner, VisualElement element, Vector2 toPixels, MotionSpec spec)
            => runner.Play(element, MotionTargets.Translate, toPixels, spec, MotionChannel.Translate);

        public static MotionHandle Fov(this IMotionRunner runner, Camera camera, float to, MotionSpec spec)
            => runner.Play(camera, MotionTargets.FieldOfView, new Vector4(to, 0f, 0f, 0f), spec, MotionChannel.Fov);

        public static MotionHandle MpbFloat(this IMotionRunner runner, Renderer renderer, int propertyId, float to, MotionSpec spec, string channel)
            => runner.Play(renderer, MotionTargets.MpbFloat(propertyId), new Vector4(to, 0f, 0f, 0f), spec, channel);

        public static MotionHandle MpbColor(this IMotionRunner runner, Renderer renderer, int propertyId, Color to, MotionSpec spec, string channel = MotionChannel.Color)
            => runner.Play(renderer, MotionTargets.MpbColor(propertyId), to, spec, channel);
    }
}
