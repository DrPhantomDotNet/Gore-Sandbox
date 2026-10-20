using UnityEngine;

namespace SolarEclipse
{
    public enum DamageType { Generic, Bullet, Buckshot, Slash, Stab, Blunt, Explosion, Fall, Crush, Admin }

    /// <summary>Everything a damage receiver needs to react to a hit.</summary>
    public struct DamageInfo
    {
        public float Amount;
        /// <summary>Impulse (N·s) applied at Point.</summary>
        public Vector3 Force;
        public Vector3 Point;
        public Vector3 Normal;
        /// <summary>Travel direction of the projectile/blade, normalized.</summary>
        public Vector3 Direction;
        public DamageType Type;
        public object Source;
        public string Weapon;
        /// <summary>Projectile diameter in mm (9, 5.56, 8.4 for buckshot). 0 for non-bullets.</summary>
        public float Caliber;
        /// <summary>Remaining penetration power 0..1+.</summary>
        public float Penetration;
        /// <summary>True when this is the exit side of a through-and-through wound.</summary>
        public bool IsExit;

        public static DamageInfo Simple(float amount, Vector3 force, Vector3 point, DamageType type, object source = null)
            => new DamageInfo
            {
                Amount = amount, Force = force, Point = point, Normal = -force.normalized,
                Direction = force.sqrMagnitude > 0.0001f ? force.normalized : Vector3.down,
                Type = type, Source = source, Weapon = type.ToString(),
            };

        public bool IsSharp => Type == DamageType.Slash || Type == DamageType.Stab;
        public bool IsBallistic => Type == DamageType.Bullet || Type == DamageType.Buckshot || Type == DamageType.Admin;
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }

    /// <summary>Surface kinds drive impact particles, decals and sounds.</summary>
    public enum SurfaceType { Concrete, Metal, Wood, Dirt, Grass, Flesh, Glass, Carpet, Water }

    /// <summary>Tag a collider with what it is made of.</summary>
    public sealed class Surface : MonoBehaviour
    {
        public SurfaceType Type = SurfaceType.Concrete;

        public static SurfaceType Of(Collider c)
        {
            if (c == null) return SurfaceType.Concrete;
            var s = c.GetComponentInParent<Surface>();
            if (s != null) return s.Type;
            if (c.gameObject.layer == Layers.Ragdoll) return SurfaceType.Flesh;
            return SurfaceType.Concrete;
        }
    }
}
