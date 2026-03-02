namespace Unidad.Core.Inventory
{
    public readonly record struct InventorySlot(ItemId ItemId, int Count)
    {
        public static readonly InventorySlot Empty = new(default, 0);

        public bool IsEmpty => Count <= 0;

        public override string ToString() => IsEmpty ? "Empty" : $"{ItemId}x{Count}";
    }
}
