using Unity.Collections;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    public struct InventoryData : IComponentData
    {
        public int BaseSlotCount;
        public int MaxSlotCount;
    }

    public struct InventorySlotElement : IBufferElementData
    {
        public int ItemId;
        public int Count;

        public bool IsEmpty => Count <= 0;

        public static InventorySlotElement Empty => new InventorySlotElement { ItemId = 0, Count = 0 };
    }

    public struct InventoryCapacityModifier : IBufferElementData
    {
        public ModifierElement Modifier;
    }

    public struct InventorySlotChanged : IComponentData, IEnableableComponent { }
    public struct InventoryFull : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Placed on a singleton entity to define all item types.
    /// </summary>
    public struct ItemDefinitionElement : IBufferElementData
    {
        public int ItemId;
        public int MaxStackSize;
        public FixedString64Bytes DisplayName;
    }
}
