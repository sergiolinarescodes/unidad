namespace Unidad.Core.Inventory
{
    public readonly record struct InventoryId(string Value)
    {
        public override string ToString() => $"Inventory({Value})";
    }
}
