#if UNIDAD_PHYSICS3D // optional module: define UNIDAD_PHYSICS3D in Player Settings to compile
using UnityEngine;

namespace Unidad.Core.Physics
{
    /// <summary>
    /// Fired when two registered physics entities begin colliding.
    /// Contact data is snapshotted at callback time — safe to consume later.
    /// </summary>
    public readonly record struct CollisionBeginEvent(
        PhysicsEntityId EntityA,
        PhysicsEntityId EntityB,
        Vector3 ContactPoint,
        Vector3 ContactNormal,
        float RelativeSpeed);

    /// <summary>
    /// Fired when two registered physics entities stop colliding.
    /// </summary>
    public readonly record struct CollisionEndEvent(
        PhysicsEntityId EntityA,
        PhysicsEntityId EntityB);

    /// <summary>
    /// Fired when a registered entity enters a registered trigger.
    /// </summary>
    public readonly record struct TriggerEnterEvent(
        PhysicsEntityId EntityId,
        PhysicsEntityId TriggerId);

    /// <summary>
    /// Fired when a registered entity exits a registered trigger.
    /// </summary>
    public readonly record struct TriggerExitEvent(
        PhysicsEntityId EntityId,
        PhysicsEntityId TriggerId);
}
#endif // UNIDAD_PHYSICS3D
