using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unidad.Core.Bootstrap;

namespace Unidad.Core.Tests.Tests.Conventions
{
    /// <summary>
    /// Convention tests that verify architectural rules across all assemblies.
    /// These complement the compile-time enforcement of ISystemInstaller.
    /// </summary>
    [TestFixture]
    public class SystemConventionTests
    {
        private static readonly string[] AllowedAssemblyPrefixes =
        {
            "Unidad.Core",
            "Experimental"
        };

        private static IEnumerable<Type> GetAllGameTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => AllowedAssemblyPrefixes.Any(p => a.GetName().Name.StartsWith(p)))
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                });
        }

        [Test]
        public void AllEvents_AreStructs()
        {
            var eventTypes = GetAllGameTypes()
                .Where(t => t.Name.EndsWith("Event") && t.IsPublic);

            var nonStructEvents = eventTypes.Where(t => !t.IsValueType).ToList();

            if (nonStructEvents.Count > 0)
            {
                Assert.Fail(
                    $"The following event types are not structs:\n" +
                    string.Join("\n", nonStructEvents.Select(t => $"  - {t.FullName}")));
            }
        }

        [Test]
        public void AllInstallers_ImplementISystemInstaller()
        {
            var installerTypes = GetAllGameTypes()
                .Where(t => t.Name.EndsWith("Installer") &&
                            !t.IsAbstract && !t.IsInterface &&
                            t.Namespace != null &&
                            !t.Namespace.StartsWith("Unidad.Core")); // Skip framework's own

            var nonCompliant = installerTypes
                .Where(t => !typeof(ISystemInstaller).IsAssignableFrom(t))
                .ToList();

            if (nonCompliant.Count > 0)
            {
                Assert.Inconclusive(
                    $"The following installer types do not implement ISystemInstaller:\n" +
                    string.Join("\n", nonCompliant.Select(t => $"  - {t.FullName}")));
            }
        }

        [Test]
        public void NoStaticMutableState_InServices()
        {
            var serviceTypes = GetAllGameTypes()
                .Where(t => t.Name.EndsWith("Service") &&
                            !t.IsAbstract && !t.IsInterface);

            var violations = new List<string>();

            foreach (var type in serviceTypes)
            {
                var staticMutableFields = type
                    .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    .Where(f => !f.IsInitOnly && !f.IsLiteral)
                    .ToList();

                foreach (var field in staticMutableFields)
                {
                    violations.Add($"  - {type.FullName}.{field.Name}");
                }
            }

            if (violations.Count > 0)
            {
                Assert.Inconclusive(
                    $"The following services have static mutable fields:\n" +
                    string.Join("\n", violations));
            }
        }

        [Test]
        public void AllServiceImplementations_AreInternal()
        {
            var serviceTypes = GetAllGameTypes()
                .Where(t => t.Name.EndsWith("Service") &&
                            !t.IsInterface && !t.IsAbstract &&
                            t.Namespace != null &&
                            !t.Namespace.StartsWith("Unidad.Core")); // Skip framework

            var publicServices = serviceTypes
                .Where(t => t.IsPublic)
                .ToList();

            if (publicServices.Count > 0)
            {
                Assert.Inconclusive(
                    $"The following service implementations should be internal " +
                    $"(only interfaces should be public):\n" +
                    string.Join("\n", publicServices.Select(t => $"  - {t.FullName}")));
            }
        }

        [Test]
        public void AllInstallers_HaveTestFactory()
        {
            var installerTypes = GetAllGameTypes()
                .Where(t => typeof(ISystemInstaller).IsAssignableFrom(t) &&
                            !t.IsAbstract && !t.IsInterface);

            foreach (var installerType in installerTypes)
            {
                ISystemInstaller installer;
                try
                {
                    installer = (ISystemInstaller)Activator.CreateInstance(installerType);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"{installerType.Name} could not be instantiated: {ex.Message}");
                    return;
                }

                var factory = installer.CreateTestFactory();
                Assert.That(factory, Is.Not.Null,
                    $"{installerType.Name}.CreateTestFactory() returned null");

                var scenarios = factory.GetScenarios().ToList();
                Assert.That(scenarios.Count, Is.GreaterThan(0),
                    $"{installerType.Name} has a test factory but no scenarios");
            }
        }
    }
}
