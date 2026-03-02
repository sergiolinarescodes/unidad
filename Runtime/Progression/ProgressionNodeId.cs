namespace Unidad.Core.Progression
{
    public readonly record struct ProgressionNodeId(string Value)
    {
        public override string ToString() => $"Node({Value})";
    }
}
