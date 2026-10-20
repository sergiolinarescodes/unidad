namespace Unidad.Core.Abstractions
{
    /// <summary>A view's read of a service's state; <see cref="Version"/> changes on every change, so a view redraws only then.</summary>
    public interface IReadModel<out T>
    {
        T Current { get; }
        int Version { get; }
    }
}
