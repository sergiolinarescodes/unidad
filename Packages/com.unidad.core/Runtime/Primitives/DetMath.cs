using System;

namespace Unidad.Core.Primitives
{
    /// <summary>
    /// Transcendental functions that return the SAME BITS on every x64 machine. <c>MathF.Sin</c>,
    /// <c>Atan2</c>, <c>Exp</c> and friends call the platform's C library, and Windows and Linux
    /// round their last digit differently: a simulation stepped on two machines diverges after one
    /// step and never converges again. An assembly marked <c>[assembly: DeterministicSimulation]</c>
    /// (analyzer rule UD009) computes through here instead.
    /// <para>
    /// Everything below is only +, -, *, / and <see cref="Math.Sqrt"/> in double, which IEEE 754
    /// requires to be correctly rounded, plus exact bit manipulation. Accuracy is ~1e-15 relative
    /// in double, so the float results are the correctly rounded value almost always.
    /// </para>
    /// </summary>
    public static class DetMath
    {
        public const double Pi = 3.141592653589793;
        private const double HalfPi = 1.5707963267948966;
        // Cody-Waite split of pi/2: k * Pio2A is exact for any |k| < 2^21, so range reduction is too.
        private const double Pio2A = 1.5707963267341256;
        private const double Pio2B = 6.077100506303966e-11;
        private const double Pio2C = 2.0222662487959506e-21;
        private const double Ln2Hi = 0.6931471803691238;
        private const double Ln2Lo = 1.9082149292705877e-10;
        private const double Ln2 = 0.6931471805599453;

        public static float Sin(float x) => (float)Sin((double)x);
        public static float Cos(float x) => (float)Cos((double)x);
        public static float Atan(float x) => (float)Atan((double)x);
        public static float Atan2(float y, float x) => (float)Atan2((double)y, (double)x);
        public static float Exp(float x) => (float)Exp((double)x);
        public static float Log(float x) => (float)Log((double)x);
        public static float Tanh(float x) => (float)Tanh((double)x);
        public static float Acos(float x) => (float)Acos((double)x);

        // System.Numerics' helpers were rewritten in .NET 9 (Lerp may fuse a multiply-add), so the
        // same step differs between .NET 8 and .NET 10. Plain scalar arithmetic here is the same on
        // every runtime.
        public static System.Numerics.Vector2 Lerp(System.Numerics.Vector2 a, System.Numerics.Vector2 b, float t)
            => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        public static float Length(System.Numerics.Vector2 v) => MathF.Sqrt(v.X * v.X + v.Y * v.Y);

        public static float Distance(System.Numerics.Vector2 a, System.Numerics.Vector2 b) => Length(a - b);

        public static System.Numerics.Vector2 Normalize(System.Numerics.Vector2 v)
        {
            var length = Length(v);
            return new System.Numerics.Vector2(v.X / length, v.Y / length);
        }

        public static double Sin(double x) => SinCos(x, cosine: false);
        public static double Cos(double x) => SinCos(x, cosine: true);

        private static double SinCos(double x, bool cosine)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return double.NaN;
            var k = Math.Round(x / HalfPi);
            var r = ((x - k * Pio2A) - k * Pio2B) - k * Pio2C;
            var quadrant = (int)((long)k & 3);
            if (cosine) quadrant = (quadrant + 1) & 3;
            switch (quadrant)
            {
                case 0: return SinPoly(r);
                case 1: return CosPoly(r);
                case 2: return -SinPoly(r);
                default: return -CosPoly(r);
            }
        }

        // |r| <= pi/4: Taylor to r^17 / r^16 leaves < 1e-17.
        private static double SinPoly(double r)
        {
            var r2 = r * r;
            var sum = 0d;
            for (var i = SinCoefficients.Length - 1; i >= 0; i--) sum = (sum + SinCoefficients[i]) * r2;
            return r + r * sum;
        }

        private static double CosPoly(double r)
        {
            var r2 = r * r;
            var sum = 0d;
            for (var i = CosCoefficients.Length - 1; i >= 0; i--) sum = (sum + CosCoefficients[i]) * r2;
            return 1d + sum;
        }

        // (-1)^k / n!, sin over n = 3, 5 .. 17 and cos over n = 2, 4 .. 16. Built once; read only.
#pragma warning disable UD014 // immutable after type init; never written
        private static readonly double[] SinCoefficients = Taylor(3);
        private static readonly double[] CosCoefficients = Taylor(2);
#pragma warning restore UD014

        private static double[] Taylor(int first)
        {
            var result = new double[8];
            for (var i = 0; i < result.Length; i++)
            {
                var n = first + 2 * i;
                var factorial = 1d;
                for (var j = 2; j <= n; j++) factorial *= j;
                result[i] = (i % 2 == 0 ? -1d : 1d) / factorial;
            }
            return result;
        }

        public static double Atan(double t)
        {
            if (double.IsNaN(t)) return double.NaN;
            var negative = t < 0;
            var a = Math.Abs(t);
            var invert = a > 1d;
            if (invert) a = 1d / a;
            // Two half-angle steps (atan t = 2 atan(t / (1 + sqrt(1 + t^2)))) bring |a| under 0.2.
            a = a / (1d + Math.Sqrt(1d + a * a));
            a = a / (1d + Math.Sqrt(1d + a * a));
            var a2 = a * a;
            var sum = 0d;
            for (var n = 27; n >= 3; n -= 2) sum = (sum + ((n / 2) % 2 == 0 ? 1d : -1d) / n) * a2;
            var result = 4d * (a + a * sum);
            if (invert) result = HalfPi - result;
            return negative ? -result : result;
        }

        /// <summary>The usual quadrants. A zero x with a zero y answers 0, as <c>MathF.Atan2</c> does
        /// for positive zeros; the sign of a zero is not honoured.</summary>
        public static double Atan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
            if (x > 0) return Atan(y / x);
            if (x < 0) return y >= 0 ? Atan(y / x) + Pi : Atan(y / x) - Pi;
            return y > 0 ? HalfPi : y < 0 ? -HalfPi : 0d;
        }

        public static double Exp(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            if (x > 709.7) return double.PositiveInfinity;
            if (x < -745.2) return 0d;
            var k = Math.Round(x / Ln2);
            var r = (x - k * Ln2Hi) - k * Ln2Lo;
            // |r| <= ln2 / 2: Taylor to r^14 leaves < 1e-17.
            var sum = 1d;
            for (var n = 14; n >= 1; n--) sum = 1d + sum * r / n;
            return ScaleB(sum, (int)k);
        }

        // Math.ScaleB is missing from netstandard2.1: multiply by exact powers of two, in two halves outside the normal range
        // so each factor is a normal double and only the last multiply rounds.
        private static double ScaleB(double x, int n)
        {
            if (n >= -1022 && n <= 1023) return x * Pow2(n);
            var half = n / 2;
            return x * Pow2(half) * Pow2(n - half);
        }

        private static double Pow2(int n) => BitConverter.Int64BitsToDouble((long)(n + 1023) << 52);

        public static double Log(double x)
        {
            if (double.IsNaN(x) || x < 0) return double.NaN;
            if (x == 0) return double.NegativeInfinity;
            if (double.IsPositiveInfinity(x)) return x;
            var exponent = 0;
            if (x < 2.2250738585072014e-308) { x *= 18014398509481984d; exponent = -54; } // 2^54, subnormals
            var bits = BitConverter.DoubleToInt64Bits(x);
            exponent += (int)((bits >> 52) & 0x7FF) - 1023;
            var m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
            if (m > 1.4142135623730951) { m *= 0.5; exponent++; }
            // ln m = 2 atanh(s), |s| <= 0.172: series to s^29 leaves < 1e-17.
            var s = (m - 1d) / (m + 1d);
            var s2 = s * s;
            var sum = 0d;
            for (var n = 29; n >= 3; n -= 2) sum = (sum + 1d / n) * s2;
            return exponent * Ln2 + 2d * (s + s * sum);
        }

        public static double Tanh(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            if (x > 20d) return 1d;
            if (x < -20d) return -1d;
            var e = Exp(2d * x);
            return (e - 1d) / (e + 1d);
        }

        public static double Acos(double x)
        {
            if (double.IsNaN(x) || x > 1d || x < -1d) return double.NaN;
            return Atan2(Math.Sqrt((1d - x) * (1d + x)), x);
        }
    }
}
