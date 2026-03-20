using Unity.Entities;

namespace Unidad.Core.DOTS
{
    public struct GridData : IComponentData
    {
        public int Width;
        public int Height;
        public float CellSize;
    }

    public struct GridCellElement : IBufferElementData
    {
        public int Value;
    }

    public struct GridCellChanged : IComponentData, IEnableableComponent { }

    public struct GridCellChangeRecord : IBufferElementData
    {
        public int Index;
        public int OldValue;
        public int NewValue;
    }
}
