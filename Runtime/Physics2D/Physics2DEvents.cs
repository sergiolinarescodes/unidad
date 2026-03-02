using UnityEngine;

namespace Unidad.Core.Physics2D
{
    public readonly record struct Collision2DBeginEvent(
        Physics2DEntityId EntityA,
        Physics2DEntityId EntityB,
        Vector2 ContactPoint,
        Vector2 ContactNormal,
        float RelativeSpeed);

    public readonly record struct Collision2DEndEvent(
        Physics2DEntityId EntityA,
        Physics2DEntityId EntityB);

    public readonly record struct Trigger2DEnterEvent(
        Physics2DEntityId EntityId,
        Physics2DEntityId TriggerId);

    public readonly record struct Trigger2DExitEvent(
        Physics2DEntityId EntityId,
        Physics2DEntityId TriggerId);
}
