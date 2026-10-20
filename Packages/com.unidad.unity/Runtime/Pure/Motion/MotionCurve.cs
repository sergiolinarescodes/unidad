using Unidad.Core.Motion;

namespace Unidad.Adapter.Motion
{
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

        public static MotionCurve ArcOf(float height) => new(MotionCurveKind.Arc, Height: height);

        public static MotionCurve PunchOf(PunchSpec punch) => new(MotionCurveKind.Punch, Punch: punch);

        public static MotionCurve ShakeOf(ShakeSpec shake, int axis) => new(MotionCurveKind.Shake, Shake: shake, Axis: axis);

        /// <summary>Punch and Shake return to rest: they end where they started.</summary>
        public bool EndsAtFrom => Kind is MotionCurveKind.Punch or MotionCurveKind.Shake;
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
}
