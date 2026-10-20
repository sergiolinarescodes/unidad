using Unidad.Core.Bootstrap;
using Unidad.Core.DI;
using Unidad.Core.Diagnostics;
using Unidad.Core.EventBus;
using Unidad.Core.HistoryService;
using UnityDebug = UnityEngine.Debug;

namespace Unidad.Adapter
{
    /// <summary>
    /// The debug mode at order 1: the real state (also bound as its concrete type) in development builds and the editor,
    /// core's always-off one in release players; and the History section.
    /// </summary>
    internal sealed class DebugModeInstaller : ISystemInstaller
    {
        public void Install(ContainerBuilder builder)
        {
            if (UnityDebug.isDebugBuild)
                builder.AddSingleton(c => new DebugModeState(c.Resolve<IEventBus>()), 1, typeof(DebugModeState), typeof(IDebugModeState));
            else
                builder.AddSingleton<IDebugModeState>(_ => new InactiveDebugMode(), 1, typeof(IDebugModeState));
            builder.AddMany<IDebugSection>(c => new HistoryDebugSection(c.TryResolve<IHistoryService>(out var history) ? history : null), int.MaxValue);
        }
    }
}
