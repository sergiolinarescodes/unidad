namespace Unidad.Core.Inventory
{
    public readonly record struct ItemDefinition(ItemId Id, string DisplayName, int MaxStackSize);
}
