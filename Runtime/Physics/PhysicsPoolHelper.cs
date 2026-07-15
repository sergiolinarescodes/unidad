#if UNIDAD_PHYSICS3D // optional module: define UNIDAD_PHYSICS3D in Player Settings to compile
using UnityEngine;

namespace Unidad.Core.Physics
{
    /// <summary>
    /// Utility methods for resetting physics state on pooled objects.
    /// Call from pool's resetAction delegate — game code, not framework magic.
    /// </summary>
    public static class PhysicsPoolHelper
    {
        /// <summary>
        /// Resets a Rigidbody to a clean state: zeroes velocity, angular velocity,
        /// resets inertia tensor, and puts the body to sleep.
        /// </summary>
        public static void ResetRigidbody(Rigidbody rb)
        {
            if (rb == null) return;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.ResetInertiaTensor();
            rb.Sleep();
        }

        /// <summary>
        /// Resets a physics GameObject: sets position/rotation and resets its Rigidbody if present.
        /// </summary>
        public static void ResetPhysicsObject(GameObject go, Vector3 position, Quaternion rotation)
        {
            if (go == null) return;

            go.transform.SetPositionAndRotation(position, rotation);

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
                ResetRigidbody(rb);
        }
    }
}
#endif // UNIDAD_PHYSICS3D
