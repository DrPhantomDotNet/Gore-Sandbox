using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Unity 6 renamed Rigidbody velocity/drag. These helpers compile on both API versions.</summary>
    public static class RB
    {
        public static Vector3 Vel(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVel(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }

        public static float LinearDamping(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearDamping;
#else
            return rb.drag;
#endif
        }
    }
}

namespace SolarEclipse
{
    /// <summary>Unity 6 renamed PhysicMaterial to PhysicsMaterial; this assigns one on either version.</summary>
    public static class PhysMat
    {
        public static void Apply(Collider c, string name, float bounciness, float friction, bool maxBounce = true)
        {
#if UNITY_6000_0_OR_NEWER
            c.sharedMaterial = new PhysicsMaterial(name)
            {
                bounciness = bounciness, dynamicFriction = friction, staticFriction = friction,
                bounceCombine = maxBounce ? PhysicsMaterialCombine.Maximum : PhysicsMaterialCombine.Average,
            };
#else
            c.sharedMaterial = new PhysicMaterial(name)
            {
                bounciness = bounciness, dynamicFriction = friction, staticFriction = friction,
                bounceCombine = maxBounce ? PhysicMaterialCombine.Maximum : PhysicMaterialCombine.Average,
            };
#endif
        }

        public static void Frictionless(Collider c)
        {
#if UNITY_6000_0_OR_NEWER
            c.sharedMaterial = new PhysicsMaterial("NoFriction") { dynamicFriction = 0, staticFriction = 0, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0 };
#else
            c.sharedMaterial = new PhysicMaterial("NoFriction") { dynamicFriction = 0, staticFriction = 0, frictionCombine = PhysicMaterialCombine.Minimum, bounciness = 0 };
#endif
        }
    }
}
