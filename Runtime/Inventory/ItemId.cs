namespace Unidad.Core.Inventory
{
    public readonly record struct ItemId(string Value)
    {
        public override string ToString() => $"Item({Value})";
    }
}
