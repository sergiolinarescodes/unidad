using Unidad.Core.EventBus;
using Unidad.Core.HistoryService;

namespace Unidad.Core.Tests.Tests.TestUtilities
{
    /// <summary>
    /// Simplified DI setup for tests. Provides pre-configured services
    /// without requiring Reflex container.
    /// </summary>
    public sealed class TestContainerBuilder
    {
        private IEventBus _eventBus;
        private IHistoryService _historyService;

        /// <summary>
        /// Creates a builder with default test services (TestEventBus with history).
        /// </summary>
        public static TestContainerBuilder Create()
        {
            var testBus = new TestEventBus();
            return new TestContainerBuilder
            {
                _eventBus = testBus,
                _historyService = testBus.History,
            };
        }

        /// <summary>
        /// Creates a builder with a lightweight MockEventBus (no history).
        /// </summary>
        public static TestContainerBuilder CreateLightweight()
        {
            return new TestContainerBuilder
            {
                _eventBus = new MockEventBus(),
                _historyService = null,
            };
        }

        public TestContainerBuilder WithEventBus(IEventBus eventBus)
        {
            _eventBus = eventBus;
            return this;
        }

        public TestContainerBuilder WithHistoryService(IHistoryService historyService)
        {
            _historyService = historyService;
            return this;
        }

        public IEventBus EventBus => _eventBus;
        public IHistoryService HistoryService => _historyService;

        /// <summary>
        /// Builds a TestDependencies instance for use in ISystemTestFactory.CreateForTesting().
        /// </summary>
        public Testing.TestDependencies BuildTestDependencies()
        {
            return new Testing.TestDependencies(_eventBus, _historyService);
        }
    }
}
