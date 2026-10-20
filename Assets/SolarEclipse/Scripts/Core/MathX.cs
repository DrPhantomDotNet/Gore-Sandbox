using UnityEngine;

namespace SolarEclipse
{
    public static class MathX
    {
        public static float Damp(float current, float target, float sharpness, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));

        public static Vector3 Damp(Vector3 current, Vector3 target, float sharpness, float dt)
            => Vector3.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));

        public static Quaternion Damp(Quaternion current, Quaternion target, float sharpness, float dt)
            => Quaternion.Slerp(current, target, 1f - Mathf.Exp(-sharpness * dt));

        public static float Remap(float v, float a0, float a1, float b0, float b1)
            => Mathf.Lerp(b0, b1, Mathf.InverseLerp(a0, a1, v));

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public static float SmoothStep01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>Signed angle around an axis in degrees, -180..180.</summary>
        public static float WrapAngle(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            if (a < -180f) a += 360f;
            return a;
        }

        public static Vector3 WrapEuler(Vector3 e) => new Vector3(WrapAngle(e.x), WrapAngle(e.y), WrapAngle(e.z));

        /// <summary>Axis * angle (radians) of the rotation from a to b, shortest path.</summary>
        public static Vector3 AngularError(Quaternion from, Quaternion to)
        {
            var q = to * Quaternion.Inverse(from);
            if (q.w < 0f) { q.x = -q.x; q.y = -q.y; q.z = -q.z; q.w = -q.w; }
            q.ToAngleAxis(out float angle, out Vector3 axis);
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x)) return Vector3.zero;
            return axis.normalized * (angle * Mathf.Deg2Rad);
        }

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }

        public static bool IsFinite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
