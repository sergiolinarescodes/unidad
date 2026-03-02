namespace Unidad.Core.Inventory
{
    public readonly record struct ItemAddedEvent(InventoryId InventoryId, ItemId ItemId, int Count, int SlotIndex);
    public readonly record struct ItemRemovedEvent(InventoryId InventoryId, ItemId ItemId, int Count, int SlotIndex);
    public readonly record struct InventoryFullEvent(InventoryId InventoryId, ItemId ItemId, int OverflowCount);
    public readonly record struct SlotChangedEvent(InventoryId InventoryId, int SlotIndex, InventorySlot OldSlot, InventorySlot NewSlot);
    public readonly record struct InventoryCreatedEvent(InventoryId InventoryId, int SlotCount);
    public readonly record struct InventoryRemovedEvent(InventoryId InventoryId);
}
