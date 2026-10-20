using System;
using System.Numerics;

namespace Unidad.Core.Motion
{
    /// <summary>A quadratic Bezier lifted by a height: a trail that reveals point by point.</summary>
    public static class Bezier2
    {
        /// <summary>(1-t)²·from + 2(1-t)t·mid + t²·to.</summary>
        public static Vector3 Point(Vector3 from, Vector3 mid, Vector3 to, float t)
        {
            float u = 1f - t;
            return u * u * from + 2f * u * t * mid + t * t * to;
        }

        /// <summary>The control point: the midpoint raised by <paramref name="height"/> along +Y.</summary>
        public static Vector3 Mid(Vector3 from, Vector3 to, float height) => (from + to) * 0.5f + Vector3.UnitY * height;

        /// <summary>Fills <paramref name="points"/> (at least 2) evenly in t from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static void Fill(Vector3 from, Vector3 to, float height, Span<Vector3> points)
        {
            if (points.Length < 2) throw new ArgumentException("A trail has at least 2 points.", nameof(points));
            var mid = Mid(from, to, height);
            int last = points.Length - 1;
            for (int i = 0; i <= last; i++) points[i] = Point(from, mid, to, (float)i / last);
        }

        /// <summary>How many of <paramref name="pointCount"/> points show at <paramref name="elapsed"/>: clamp(ceil(elapsed/duration·n), 1, n); n when duration &lt;= 0.</summary>
        public static int RevealCount(float elapsed, float duration, int pointCount)
        {
            if (pointCount <= 0) return 0;
            if (duration <= 0f) return pointCount;
            float shown = MathF.Ceiling(elapsed / duration * pointCount);
            return shown < 1f ? 1 : shown > pointCount ? pointCount : (int)shown;
        }
    }
}
