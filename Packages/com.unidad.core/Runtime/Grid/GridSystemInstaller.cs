using System;
using Unidad.Core.Bootstrap;
using Unidad.Core.DI;

namespace Unidad.Core.Grid
{
    /// <summary>Binds <see cref="IGridFactory"/>.</summary>
    public sealed class GridSystemInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder) => throw new NotImplementedException();
    }
}
