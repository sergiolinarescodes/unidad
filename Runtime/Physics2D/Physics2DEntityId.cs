namespace Unidad.Core.Physics2D
{
    /// <summary>
    /// Lightweight identifier for 2D physics-registered entities.
    /// </summary>
    public readonly record struct Physics2DEntityId(int Value)
    {
        public static readonly Physics2DEntityId None = new(0);

        public bool IsValid => Value != 0;

        public override string ToString() => $"Physics2DEntity({Value})";
    }
}
