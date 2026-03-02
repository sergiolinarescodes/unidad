namespace Unidad.Core.Progression
{
    public readonly record struct ProgressionTreeId(string Value)
    {
        public override string ToString() => $"Tree({Value})";
    }
}
