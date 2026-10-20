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

        public static float Sin(float x) => throw new NotImplementedException();
        public static float Cos(float x) => throw new NotImplementedException();
        public static float Atan(float x) => throw new NotImplementedException();
        public static float Atan2(float y, float x) => throw new NotImplementedException();
        public static float Exp(float x) => throw new NotImplementedException();
        public static float Log(float x) => throw new NotImplementedException();
        public static float Tanh(float x) => throw new NotImplementedException();
        public static float Acos(float x) => throw new NotImplementedException();
        public static System.Numerics.Vector2 Lerp(System.Numerics.Vector2 a, System.Numerics.Vector2 b, float t) => throw new NotImplementedException();
        public static float Length(System.Numerics.Vector2 v) => throw new NotImplementedException();
        public static float Distance(System.Numerics.Vector2 a, System.Numerics.Vector2 b) => throw new NotImplementedException();
        public static System.Numerics.Vector2 Normalize(System.Numerics.Vector2 v) => throw new NotImplementedException();
        public static double Sin(double x) => throw new NotImplementedException();
        public static double Cos(double x) => throw new NotImplementedException();
        public static double Atan(double t) => throw new NotImplementedException();
        public static double Atan2(double y, double x) => throw new NotImplementedException();
        public static double Exp(double x) => throw new NotImplementedException();
        public static double Log(double x) => throw new NotImplementedException();
        public static double Tanh(double x) => throw new NotImplementedException();
        public static double Acos(double x) => throw new NotImplementedException();
    }
}
