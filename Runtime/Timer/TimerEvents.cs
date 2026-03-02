namespace Unidad.Core.Timer
{
    public readonly record struct TimerCompletedEvent(TimerHandle Handle);
    public readonly record struct TimerCancelledEvent(TimerHandle Handle);
}
