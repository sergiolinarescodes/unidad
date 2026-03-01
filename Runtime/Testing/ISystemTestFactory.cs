using System;
using System.Collections.Generic;

namespace Unidad.Core.Testing
{
    /// <summary>
    /// Factory that every system must provide for testing.
    /// Declares what services it tests, creates isolated instances, and provides scenarios.
    /// </summary>
    public interface ISystemTestFactory
    {
        /// <summary>Services this factory covers. For traceability.</summary>
        Type[] TestedServices { get; }

        /// <summary>Creates an isolated service instance for testing.</summary>
        object CreateForTesting(TestDependencies deps);

        /// <summary>Returns all test scenarios for this system.</summary>
        IEnumerable<ITestScenario> GetScenarios();
    }
}
