using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Linear and angular velocity for simple physics simulation.
    /// Used by any entity that participates in custom physics (gravity, collision, etc.).
    /// </summary>
    public struct Velocity : IComponentData
    {
        public float3 Linear;
        public float3 Angular;
    }

    /// <summary>
    /// Per-entity physics properties. Defines how the entity interacts with forces and collisions.
    /// </summary>
    public struct PhysicsBody : IComponentData
    {
        public float Mass;
        public float Bounciness;
        public float Drag;
        public float GravityScale;

        public static PhysicsBody Default => new()
        {
            Mass = 1f,
            Bounciness = 0.5f,
            Drag = 0.1f,
            GravityScale = 1f
        };
    }

    /// <summary>
    /// Base color for hover/highlight restore. Stores the entity's "unmodified" color
    /// so InstanceColor can be temporarily changed and restored.
    /// </summary>
    public struct BaseColor : IComponentData
    {
        public float4 Value;
    }

    /// <summary>
    /// Enableable tag: entity is currently hovered by the mouse cursor.
    /// </summary>
    public struct Hovered : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Singleton: defines an axis-aligned bounding box container that constrains entities.
    /// Supports rotation — physics systems transform to local space for bounds checks.
    /// </summary>
    public struct BoundsContainer : IComponentData
    {
        public float3 HalfExtents;
        public float3 Center;
        public quaternion Rotation;
    }

    /// <summary>
    /// Singleton: global physics configuration defaults.
    /// Per-entity PhysicsBody overrides these when present.
    /// </summary>
    public struct PhysicsConfig : IComponentData
    {
        public float GravityScale;
        public float Bounciness;
        public float Drag;

        public static PhysicsConfig Default => new()
        {
            GravityScale = 1f,
            Bounciness = 0.4f,
            Drag = 0.5f
        };
    }
}
