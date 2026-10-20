using System;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;

namespace Unidad.Flow
{
    /// <summary>Binds <see cref="IFlowService"/>, <see cref="ISceneSwitchService"/> and <see cref="IInputFocusService"/>.</summary>
    public sealed class FlowInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder) => throw new NotImplementedException();
    }
}
