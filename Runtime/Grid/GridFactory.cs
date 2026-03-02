using Unidad.Core.EventBus;

namespace Unidad.Core.Grid
{
    internal sealed class GridFactory : IGridFactory
    {
        private readonly IEventBus _eventBus;

        public GridFactory(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public IGrid<TCell> Create<TCell>(int width, int height, float cellSize)
        {
            return new Grid<TCell>(width, height, cellSize, _eventBus);
        }
    }
}
