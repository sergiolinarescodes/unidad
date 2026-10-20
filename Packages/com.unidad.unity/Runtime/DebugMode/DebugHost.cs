using System;
using Unidad.Adapter.Ui;
using Unidad.Core.DI;
using Unidad.Launch;

namespace Unidad.Adapter
{
    /// <summary>The Alt+D debug window in development builds; release players bind the inactive debug mode.</summary>
    internal static class DebugHost
    {
        public static void Bind(Container container, IGameObjectFactory factory, IPanelLibrary panels, LaunchArgs launch)
            => throw new NotImplementedException();
    }
}
