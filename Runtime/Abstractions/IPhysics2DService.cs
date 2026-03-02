using System.Collections.Generic;
using Unidad.Core.Physics2D;
using UnityEngine;

namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Abstraction over Unity's 2D Physics system.
    /// Mirrors IPhysicsService for 2D projects.
    /// </summary>
    public interface IPhysics2DService
    {
        bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hit, float maxDistance);
        bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hit, float maxDistance, int layerMask);
        IReadOnlyList<Collider2D> OverlapCircle(Vector2 center, float radius);
        IReadOnlyList<Collider2D> OverlapCircle(Vector2 center, float radius, int layerMask);
        bool CheckCircle(Vector2 center, float radius);
        Physics2DEntityId RegisterEntity(GameObject gameObject, string tag);
        void UnregisterEntity(Physics2DEntityId id);
        Physics2DEntityId GetEntityId(GameObject gameObject);
    }
}
