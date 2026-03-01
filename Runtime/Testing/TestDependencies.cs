using Unidad.Core.EventBus;
using Unidad.Core.HistoryService;

namespace Unidad.Core.Testing
{
    /// <summary>
    /// Dependencies injectable into tests.
    /// Provides core services that test factories need to create isolated instances.
    /// </summary>
    public sealed class TestDependencies
    {
        public IEventBus EventBus { get; }
        public IHistoryService HistoryService { get; }

        public TestDependencies(IEventBus eventBus, IHistoryService historyService)
        {
            EventBus = eventBus;
            HistoryService = historyService;
        }
    }
}
