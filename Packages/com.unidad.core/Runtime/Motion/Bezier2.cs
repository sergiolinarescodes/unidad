using System;
using System.Numerics;

namespace Unidad.Core.Motion
{
    /// <summary>A quadratic Bezier lifted by a height: a trail that reveals point by point.</summary>
    public static class Bezier2
    {
        /// <summary>(1-t)²·from + 2(1-t)t·mid + t²·to.</summary>
        public static Vector3 Point(Vector3 from, Vector3 mid, Vector3 to, float t) => throw new NotImplementedException();

        /// <summary>The control point: the midpoint raised by <paramref name="height"/> along +Y.</summary>
        public static Vector3 Mid(Vector3 from, Vector3 to, float height) => throw new NotImplementedException();

        /// <summary>Fills <paramref name="points"/> (at least 2) evenly in t from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static void Fill(Vector3 from, Vector3 to, float height, Span<Vector3> points) => throw new NotImplementedException();

        /// <summary>How many of <paramref name="pointCount"/> points show at <paramref name="elapsed"/>: clamp(ceil(elapsed/duration·n), 1, n); n when duration &lt;= 0.</summary>
        public static int RevealCount(float elapsed, float duration, int pointCount) => throw new NotImplementedException();
    }
}
