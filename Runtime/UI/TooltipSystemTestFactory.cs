using System;
using System.Collections.Generic;
using Unidad.Core.Testing;
using Unidad.Core.UI.TextAnimation.ElementAnimation;
using Unidad.Core.UI.Tooltip;
using Unidad.Core.UI.Tooltip.Scenarios;

namespace Unidad.Core.UI
{
    internal sealed class TooltipSystemTestFactory : ISystemTestFactory
    {
        public Type[] TestedServices => new[] { typeof(ITooltipService) };

        public object CreateForTesting(TestDependencies deps)
        {
            var elementAnimator = new ElementAnimator();
            return new TooltipService(deps.EventBus, elementAnimator);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new TooltipScreenSpaceScenario();
            yield return new TooltipWorldSpaceScenario();
        }
    }
}
