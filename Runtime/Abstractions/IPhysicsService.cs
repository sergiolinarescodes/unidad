using System.Collections.Generic;
using UnityEngine;

namespace Unidad.Core.Abstractions
{
    /// <summary>
    /// Abstraction over Unity's Physics system.
    /// Production: delegates to UnityEngine.Physics.
    /// Tests: returns configurable mock results.
    /// For games needing real physics in tests, use [UnityTest] Play Mode tests.
    /// </summary>
    public interface IPhysicsService
    {
        bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance);
        IReadOnlyList<Collider> OverlapSphere(Vector3 center, float radius);
        bool CheckSphere(Vector3 center, float radius);
    }
}
