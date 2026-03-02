namespace Unidad.Core.Resource
{
    public readonly record struct ResourceId(string Value)
    {
        public override string ToString() => Value;
    }
}
