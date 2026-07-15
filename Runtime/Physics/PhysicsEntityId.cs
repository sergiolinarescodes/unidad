#if UNIDAD_PHYSICS3D // optional module: define UNIDAD_PHYSICS3D in Player Settings to compile
namespace Unidad.Core.Physics
{
    /// <summary>
    /// Lightweight identifier for physics-registered entities.
    /// Carried by collision events instead of object references.
    /// Game code looks up objects via IPhysicsEntityRegistry when needed.
    /// </summary>
    public readonly record struct PhysicsEntityId(int Value)
    {
        public static readonly PhysicsEntityId None = new(0);

        public bool IsValid => Value != 0;

        public override string ToString() => $"PhysicsEntity({Value})";
    }
}
#endif // UNIDAD_PHYSICS3D
