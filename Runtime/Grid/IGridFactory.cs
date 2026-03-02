namespace Unidad.Core.Grid
{
    public interface IGridFactory
    {
        IGrid<TCell> Create<TCell>(int width, int height, float cellSize);
    }
}
