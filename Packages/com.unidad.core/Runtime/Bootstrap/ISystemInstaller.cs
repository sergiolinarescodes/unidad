using Unidad.Core.DI;

namespace Unidad.Core.Bootstrap
{
    /// <summary>
    /// One feature's registrations. Discovered in every assembly that carries <c>[assembly: UnidadModule]</c> and installed
    /// in a fixed order (<see cref="InstallerDiscovery.FindInstallerTypes"/>).
    /// </summary>
    public interface ISystemInstaller
    {
        /// <summary>Install system services into the DI container.</summary>
        void Install(ContainerBuilder builder);
    }
}
