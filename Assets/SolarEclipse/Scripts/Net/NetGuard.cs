using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// One-line hooks the gameplay code calls so the same code works offline, as host and as client.
    /// </summary>
    public static class NetGuard
    {
        /// <summary>
        /// Call first thing in IDamageable.TakeDamage of world objects. Host: tells clients to replay the hit.
        /// Client: the host owns the object, so the hit is sent as a request (if it came from this player) and
        /// the caller must return. Returns true when the caller must stop.
        /// </summary>
        public static bool Intercept(Component c, DamageInfo info)
        {
            if (!NetState.Active || c == null) return false;
            var e = c.GetComponentInParent<NetEntity>();
            if (e == null) return false;
            if (NetState.IsClient)
            {
                if (NetState.ApplyingRemote) return false;
                if (NetState.LocalAction) NetManager.I.RequestDamageEntity(e, info);
                return true;
            }
            if (info.Amount >= 0.5f) NetManager.I.BroadcastEntityDamage(e, info);
            return false;
        }
    }

    /// <summary>Pushes a rigidbody; on a client, pushes on host-owned objects are sent to the host.</summary>
    public static class NetPhysics
    {
        public static void Impulse(Rigidbody rb, Vector3 impulse, Vector3 point)
        {
            if (rb == null) return;
            if (NetState.IsClient && !NetState.ApplyingRemote && NetManager.Handle(rb, out _, out _))
            {
                if (NetState.LocalAction) NetManager.I.RequestImpulse(rb, impulse, point);
                return;
            }
            if (!rb.isKinematic) rb.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }
    }
}
