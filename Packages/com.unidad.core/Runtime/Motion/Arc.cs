using System.Numerics;

namespace Unidad.Core.Motion
{
    /// <summary>A thrown object's path: the eased chord plus a parabola of <c>4·t·(1-t)·height</c> on linear t. +Y is up.</summary>
    public static class Arc
    {
        public static Vector3 Sample(Vector3 from, Vector3 to, float height, float t, Ease ease = Ease.OutCubic)
        {
            float linear = t > 0f ? (t < 1f ? t : 1f) : 0f;
            var position = Vector3.Lerp(from, to, Easing.Evaluate(ease, linear));
            position.Y += 4f * linear * (1f - linear) * height;
            return position;
        }
    }
}
