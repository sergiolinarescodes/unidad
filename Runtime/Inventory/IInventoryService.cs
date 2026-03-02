using Unidad.Core.Patterns.Modifier;

namespace Unidad.Core.Inventory
{
    public interface IInventoryService
    {
        void Create(InventoryId id, InventoryDefinition definition);
        bool Exists(InventoryId id);
        void Remove(InventoryId id);
        void DefineItem(ItemDefinition definition);
        bool HasItemDefinition(ItemId itemId);
        int Add(InventoryId inventoryId, ItemId itemId, int count = 1);
        bool TryRemove(InventoryId inventoryId, ItemId itemId, int count = 1);
        int GetCount(InventoryId inventoryId, ItemId itemId);
        bool Contains(InventoryId inventoryId, ItemId itemId, int count = 1);
        InventorySlot GetSlot(InventoryId inventoryId, int slotIndex);
        int GetSlotCount(InventoryId inventoryId);
        int GetUsedSlotCount(InventoryId inventoryId);
        int GetFreeSlotCount(InventoryId inventoryId);
        bool IsFull(InventoryId inventoryId);
        void SwapSlots(InventoryId srcInv, int srcSlot, InventoryId dstInv, int dstSlot);
        void MoveSlot(InventoryId srcInv, int srcSlot, InventoryId dstInv, int dstSlot);
        ModifierStack<float> GetCapacityModifiers(InventoryId inventoryId);
    }
}
