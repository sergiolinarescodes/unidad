namespace Unidad.Core.Resource
{
    public readonly record struct ResourceChangedEvent(ResourceId Id, float OldValue, float NewValue, float Max);
    public readonly record struct ResourceDepletedEvent(ResourceId Id);
    public readonly record struct ResourceFilledEvent(ResourceId Id);
}
