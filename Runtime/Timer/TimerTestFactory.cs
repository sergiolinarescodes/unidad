using System;
using System.Collections.Generic;
using Unidad.Core.Testing;

namespace Unidad.Core.Timer
{
    internal sealed class TimerTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[] { typeof(ITimerService) };

        public object CreateForTesting(TestDependencies deps)
        {
            return new TimerService(deps.EventBus);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new Scenarios.TimerScenario();
        }
    }
}
