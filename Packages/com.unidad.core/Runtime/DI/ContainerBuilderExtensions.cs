namespace Unidad.Core.DI
{
    /// <summary>
    /// Convenience extensions over the resolved <see cref="Container"/>.
    /// Kept for source parity with the Unity framework's ContainerBuilderExtensions.
    /// </summary>
    public static class ContainerBuilderExtensions
    {
        /// <summary>
        /// Attempts to resolve a service. Returns true and the resolved instance if registered,
        /// or false and default if not registered.
        /// </summary>
        public static bool TryResolve<T>(this Container container, out T result)
        {
            if (container.HasBinding<T>())
            {
                result = container.Resolve<T>();
                return true;
            }

            result = default;
            return false;
        }
    }
}
