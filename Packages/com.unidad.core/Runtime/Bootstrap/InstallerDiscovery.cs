using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unidad.Core.Diagnostics;

namespace Unidad.Core.Bootstrap
{
    /// <summary>
    /// Discovers ISystemInstaller implementations via reflection, for a bootstrap that does not list
    /// its installers by hand. Engine-free reflection over an assembly list the adapter supplies; core never touches the AppDomain.
    /// </summary>
    public static class InstallerDiscovery
    {
        /// <summary>
        /// The assemblies of <paramref name="assemblies"/> that carry <c>[assembly: UnidadModule]</c>, sorted by module name
        /// (ordinal). An assembly whose attributes cannot be read is logged and skipped.
        /// </summary>
        public static IReadOnlyList<Assembly> ModuleAssemblies(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            var modules = new List<(string Name, Assembly Assembly)>();
            foreach (var assembly in assemblies)
            {
                var name = ModuleName(assembly);
                if (name != null) modules.Add((name, assembly));
            }
            modules.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return modules.Select(m => m.Assembly).ToList();
        }

        /// <summary>Same as <see cref="ModuleAssemblies"/>.</summary>
        public static IReadOnlyList<Assembly> Modules(IEnumerable<Assembly> loaded) => ModuleAssemblies(loaded);

        /// <summary>
        /// Every concrete ISystemInstaller type in <paramref name="assemblies"/>, sorted by the assembly's module name (an
        /// assembly without the attribute sorts by its simple name), then by type FullName, both ordinal. An assembly whose
        /// types only partly load still contributes the ones that did, and the loader exceptions are logged; an assembly that
        /// cannot be enumerated at all is logged as an error. Nothing is dropped silently.
        /// </summary>
        public static IReadOnlyList<Type> FindInstallerTypes(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            var found = new List<(string Module, string Type, Type Installer)>();
            foreach (var assembly in assemblies)
            {
                var module = ModuleName(assembly) ?? Name(assembly);
                foreach (var type in LoadableTypes(assembly))
                    if (!type.IsAbstract && !type.IsInterface && typeof(ISystemInstaller).IsAssignableFrom(type))
                        found.Add((module, type.FullName, type));
            }
            found.Sort((a, b) =>
            {
                var byModule = string.CompareOrdinal(a.Module, b.Module);
                return byModule != 0 ? byModule : string.CompareOrdinal(a.Type, b.Type);
            });
            return found.Select(f => f.Installer).ToList();
        }

        private static string ModuleName(Assembly assembly)
        {
            try
            {
                return assembly.GetCustomAttribute<UnidadModuleAttribute>()?.Name;
            }
            catch (Exception ex)
            {
                Log.Warning($"[InstallerDiscovery] {Name(assembly)}: its attributes could not be read, so it is skipped. {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        internal static Type[] LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loaded = ex.Types.Where(t => t != null).ToArray();
                Log.Warning($"[InstallerDiscovery] {Name(assembly)}: {ex.Types.Length - loaded.Length} type(s) failed to load; " +
                            $"installers among them are not discovered. {loaded.Length} loaded type(s) are still scanned.");
                foreach (var message in ex.LoaderExceptions.Where(e => e != null).Select(e => e.Message).Distinct())
                    Log.Warning($"[InstallerDiscovery] {Name(assembly)} loader exception: {message}");
                return loaded;
            }
            catch (Exception ex)
            {
                Log.Error($"[InstallerDiscovery] {Name(assembly)}: its types could not be enumerated, so no installer in it is discovered. " +
                          $"{ex.GetType().Name}: {ex.Message}");
                Log.Exception(ex);
                return Array.Empty<Type>();
            }
        }

        private static string Name(Assembly assembly)
        {
            try { return assembly.GetName().Name; }
            catch (Exception) { return assembly.ToString(); }
        }

        /// <summary>
        /// Instantiates an ISystemInstaller from its type. Throws
        /// <see cref="InstallerLoadException"/> if instantiation fails — deliberately NOT a null
        /// return, so a broken installer cannot be silently skipped by a
        /// <c>if (installer != null)</c> at the call site and resurface later as a mystery
        /// <c>Resolve&lt;IFooService&gt;</c> throw.
        /// </summary>
        public static ISystemInstaller CreateInstaller(Type installerType)
        {
            if (installerType == null) throw new ArgumentNullException(nameof(installerType));

            try
            {
                return (ISystemInstaller)Activator.CreateInstance(installerType);
            }
            catch (Exception ex)
            {
                throw new InstallerLoadException(installerType, ex);
            }
        }
    }
}
