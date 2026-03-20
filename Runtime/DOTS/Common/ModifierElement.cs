using Unity.Entities;

namespace Unidad.Core.DOTS
{
    public enum ModifierOp : byte
    {
        Add,
        Multiply,
        Override,
        ClampMin,
        ClampMax
    }

    public struct ModifierElement : IBufferElementData
    {
        public int Id;
        public int Priority;
        public float Value;
        public ModifierOp Op;
        public bool IsActive;
    }
}

