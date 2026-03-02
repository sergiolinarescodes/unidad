using UnityEngine;

namespace Unidad.Core.Physics2D
{
    /// <summary>
    /// Utility methods for resetting 2D physics state on pooled objects.
    /// </summary>
    public static class Physics2DPoolHelper
    {
        public static void ResetRigidbody2D(Rigidbody2D rb)
        {
            if (rb == null) return;

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.Sleep();
        }

        public static void ResetPhysicsObject2D(GameObject go, Vector2 position, float rotation)
        {
            if (go == null) return;

            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotation);

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
                ResetRigidbody2D(rb);
        }
    }
}
