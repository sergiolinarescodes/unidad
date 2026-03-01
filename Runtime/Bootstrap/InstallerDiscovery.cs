using System;
using System.Collections.Generic;
using System.Linq;

namespace Unidad.Core.Bootstrap
{
    /// <summary>
    /// Shared utility for discovering ISystemInstaller implementations via reflection.
    /// Used by ScenarioTestHelper, AllSystemScenariosTests, and ScenarioBrowserWindow.
    /// </summary>
    public static class InstallerDiscovery
    {
        /// <summary>
        /// Discovers all concrete ISystemInstaller types across loaded assemblies.
        /// </summary>
        public static IEnumerable<Type> FindInstallerTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => !t.IsAbstract && !t.IsInterface &&
                            typeof(ISystemInstaller).IsAssignableFrom(t));
        }

        /// <summary>
        /// Instantiates an ISystemInstaller from its type. Returns null if instantiation fails.
        /// </summary>
        public static ISystemInstaller CreateInstaller(Type installerType)
        {
            try
            {
                return (ISystemInstaller)Activator.CreateInstance(installerType);
            }
            catch
            {
                return null;
            }
        }
    }
}
