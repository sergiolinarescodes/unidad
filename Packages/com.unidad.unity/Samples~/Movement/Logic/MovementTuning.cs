namespace Unidad.Samples.Movement
{
    /// <summary>The sample's numbers (10's, unchanged): the level, the physics, the input speeds and the view scale.</summary>
    public static class MovementTuning
    {
        public const float BoxHalf = 0.5f;
        public const float PlatformTopY = 0.5f;
        public const float RestY = PlatformTopY + BoxHalf;
        public const float LeftPlatformMinX = -7f, LeftPlatformMaxX = -1f;
        public const float RightPlatformMinX = 2f, RightPlatformMaxX = 8f;
        public const float SpawnX = -4f;
        public const float Gravity = -9.81f;
        public const float FellOffThreshold = -3f;

        public const float MoveSpeed = 6f;
        public const float JumpForce = 12f;

        public const float PixelsPerUnit = 48f;
        public const float OriginX = 480f, OriginY = 400f;
    }
}
