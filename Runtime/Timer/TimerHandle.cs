namespace Unidad.Core.Timer
{
    public readonly record struct TimerHandle(int Id)
    {
        public static readonly TimerHandle None = new(0);
        public bool IsValid => Id != 0;
    }
}
