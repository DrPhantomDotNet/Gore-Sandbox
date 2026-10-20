using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// ConfigurableJoint angular limits are measured in a frame whose sign is easy to get backwards.
    /// At startup we run a tiny physics experiment once and record which way +X is measured,
    /// so knees and elbows always bend the right way.
    /// </summary>
    public static class JointCalibration
    {
        static float sign;

        public static float XSign
        {
            get
            {
                if (sign == 0f) Run();
                return sign;
            }
        }

        public static void Run()
        {
            sign = -1f; // fallback matches the well-known "targetRotation is inverted" behaviour
            var root = new GameObject("JointCalibration");
            root.transform.position = new Vector3(0, -2000, 0);
            try
            {
                var a = new GameObject("A");
                a.transform.SetParent(root.transform, false);
                var ra = a.AddComponent<Rigidbody>();
                ra.isKinematic = true;
                var b = new GameObject("B");
                b.transform.SetParent(root.transform, false);
                var rb = b.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.mass = 1f;
                RB.SetDamping(rb, 0f, 0f);
                rb.maxAngularVelocity = 50f;
                var j = b.AddComponent<ConfigurableJoint>();
                j.connectedBody = ra;
                j.axis = Vector3.right;
                j.secondaryAxis = Vector3.up;
                j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
                j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Locked;
                j.angularXMotion = ConfigurableJointMotion.Limited;
                j.lowAngularXLimit = new SoftJointLimit { limit = -2f };
                j.highAngularXLimit = new SoftJointLimit { limit = 60f };
#if UNITY_2022_2_OR_NEWER
                var mode = Physics.simulationMode;
                Physics.simulationMode = SimulationMode.Script;
#else
                bool auto = Physics.autoSimulation;
                Physics.autoSimulation = false;
#endif
                for (int i = 0; i < 40; i++)
                {
                    // Spin B positively about +X (same convention as Quaternion.AngleAxis).
                    rb.angularVelocity = Vector3.right * 4f;
                    Physics.Simulate(0.02f);
                }
#if UNITY_2022_2_OR_NEWER
                Physics.simulationMode = mode;
#else
                Physics.autoSimulation = auto;
#endif
                // AngleAxis(+θ, right) * up tilts "up" toward +Z. If it reached ~60°, the joint measures +θ as positive.
                float tilt = Vector3.SignedAngle(Vector3.up, b.transform.up, Vector3.right);
                sign = tilt > 20f ? 1f : -1f;
                Debug.Log($"[JointCalibration] +X rotation reached {tilt:0.0}° > limit sign {sign}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[JointCalibration] failed, using default: " + e.Message);
            }
            Object.DestroyImmediate(root);
        }

        /// <summary>Applies limits given in the natural convention (+X = rotate child by AngleAxis(+x, right)).</summary>
        public static void SetXLimits(ConfigurableJoint j, float min, float max)
        {
            float s = XSign;
            float lo = s > 0 ? min : -max, hi = s > 0 ? max : -min;
            j.lowAngularXLimit = new SoftJointLimit { limit = Mathf.Clamp(lo, -177f, 177f) };
            j.highAngularXLimit = new SoftJointLimit { limit = Mathf.Clamp(hi, -177f, 177f) };
        }
    }
}
