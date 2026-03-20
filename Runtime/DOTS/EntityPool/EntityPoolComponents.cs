using Unity.Entities;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Tag: marks an entity as a pool prototype (the template to clone from).
    /// </summary>
    public struct PoolPrototype : IComponentData
    {
        public int PoolId;
        public int PrewarmCount;
    }

    /// <summary>
    /// Tag: marks an entity as a pooled instance belonging to a specific pool.
    /// </summary>
    public struct Pooled : IComponentData
    {
        public int PoolId;
    }

    /// <summary>
    /// Enableable: when enabled, the entity is "acquired" (in use).
    /// When disabled, the entity is "in pool" (available).
    /// Entities also receive the built-in Disabled component when returned.
    /// </summary>
    public struct PoolActive : IComponentData, IEnableableComponent { }

    /// <summary>
    /// 1-frame event: entity was just acquired this frame.
    /// </summary>
    public struct PoolAcquired : IComponentData, IEnableableComponent { }

    /// <summary>
    /// 1-frame event: entity was just returned this frame.
    /// </summary>
    public struct PoolReturned : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Add to any entity to request acquisition from a pool.
    /// The EntityPoolSystem processes these and provides the result.
    /// </summary>
    public struct AcquireRequest : IComponentData
    {
        public int PoolId;
    }

    /// <summary>
    /// Enable on a pooled entity to request return to pool.
    /// </summary>
    public struct ReturnRequest : IComponentData, IEnableableComponent { }
}
