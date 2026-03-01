using Reflex.Core;

namespace Unidad.Core.DI
{
    /// <summary>
    /// Extension methods for Reflex DI Container.
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
