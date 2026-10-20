namespace Unidad.Flow
{
    public static class FlowTuning
    {
        public const float LoadingTimeoutSeconds = 30f;
        public const int SettledTicks = 12;
        public const float CurtainLiftSeconds = 0.35f;

        /// <summary>One pulse half-cycle of the curtain mark (alpha 1 to 0.3), for the adapter.</summary>
        public const float CurtainPulseSeconds = 0.9f;
    }
}
