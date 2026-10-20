namespace Unidad.Core.Jobs
{
    /// <summary>How much background work a scene runs at once, and how much of it lands per frame.</summary>
    public sealed record JobTuning(int MaxConcurrency, int DeliveriesPerTick)
    {
        public const int DefaultMaxConcurrency = 2;
        public const int DefaultDeliveriesPerTick = 16;

        public static readonly JobTuning Default = new(DefaultMaxConcurrency, DefaultDeliveriesPerTick);
    }
}
