using Unidad.Core.Testing;

namespace Unidad.Core.Bootstrap
{
    /// <summary>
    /// Interface that every system installer MUST implement.
    /// Enforces at compile-time that every system has:
    /// 1. An Install method for DI registration
    /// 2. A CreateTestFactory method that provides test coverage
    ///
    /// If a system doesn't implement this interface, it cannot be installed
    /// through the standard bootstrap flow.
    /// </summary>
    public interface ISystemInstaller
    {
        /// <summary>
        /// Install system services into the DI container.
        /// Implementors should use static abstract when C# 11 is available.
        /// For now, use instance methods.
        /// </summary>
        void Install(Reflex.Core.ContainerBuilder builder);

        /// <summary>
        /// Create a test factory for this system.
        /// This is the enforcement mechanism: no test factory = no compilation.
        /// </summary>
        ISystemTestFactory CreateTestFactory();
    }
}
