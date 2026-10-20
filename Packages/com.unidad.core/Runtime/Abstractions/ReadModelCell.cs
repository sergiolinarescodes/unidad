namespace Unidad.Core.Abstractions
{
    /// <summary>The simplest <see cref="IReadModel{T}"/>: one value whose version bumps on every <see cref="Set"/>, even to an equal value.</summary>
    public sealed class ReadModelCell<T> : IReadModel<T>
    {
        public T Current { get; private set; }
        public int Version { get; private set; }

        public void Set(T value)
        {
            Current = value;
            Version++;
        }
    }
}
