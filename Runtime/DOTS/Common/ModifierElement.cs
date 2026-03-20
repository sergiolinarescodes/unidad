using System.Runtime.InteropServices;
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
        [MarshalAs(UnmanagedType.U1)]
        public bool IsActive;
    }
}

