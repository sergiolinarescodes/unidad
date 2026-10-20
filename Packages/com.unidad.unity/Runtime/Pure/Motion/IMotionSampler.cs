using System.Numerics;
using Unidad.Core.Motion;
using Unidad.Adapter.Motion;

namespace Unidad.Adapter
{
    /// <summary>The value of a motion at <c>elapsed</c> (delay included); the table never asks past the end.</summary>
    internal interface IMotionSampler
    {
        Vector4 Sample(in MotionCurve curve, Vector4 from, Vector4 to, MotionSpec spec, float elapsed);
    }

    /// <summary>One running motion's engine side: the target and its adapter, plus the completion callback.</summary>
    internal interface IMotionBinding
    {
        /// <summary>False once the target is destroyed: the slot is freed silently.</summary>
        bool IsAlive { get; }

        Vector4 Read();

        void Write(Vector4 value);

        void Completed();
    }

    /// <summary>The production sampler over core's Motion kit.</summary>
    internal sealed class CoreMotionSampler : IMotionSampler
    {
        public Vector4 Sample(in MotionCurve curve, Vector4 from, Vector4 to, MotionSpec spec, float elapsed)
        {
            var local = elapsed - spec.Delay;
            switch (curve.Kind)
            {
                case MotionCurveKind.Arc:
                {
                    var rawT = spec.Duration <= 0f ? 1f : System.Math.Clamp(local / spec.Duration, 0f, 1f);
                    var xyz = Arc.Sample(new Vector3(from.X, from.Y, from.Z), new Vector3(to.X, to.Y, to.Z), curve.Height, rawT, spec.Ease);
                    return new Vector4(xyz, Easing.Lerp(from.W, to.W, spec.Progress(elapsed)));
                }
                case MotionCurveKind.Punch:
                    return from * curve.Punch.Sample(local);
                case MotionCurveKind.Shake:
                {
                    var offset = curve.Shake.Sample(local);
                    var result = from;
                    switch (curve.Axis)
                    {
                        case 0: result.X += offset; break;
                        case 1: result.Y += offset; break;
                        case 2: result.Z += offset; break;
                        default: result.W += offset; break;
                    }

                    return result;
                }
                default:
                    return Vector4.Lerp(from, to, spec.Progress(elapsed));
            }
        }
    }
}
