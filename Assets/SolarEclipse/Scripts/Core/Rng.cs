using UnityEngine;

namespace SolarEclipse
{
    public static class Rng
    {
        public static float Range(float a, float b) => Random.Range(a, b);
        public static int Range(int a, int b) => Random.Range(a, b);
        public static bool Chance(float p) => Random.value < p;
        public static T Pick<T>(T[] arr) => arr[Random.Range(0, arr.Length)];
        public static T Pick<T>(System.Collections.Generic.IList<T> list) => list[Random.Range(0, list.Count)];
        public static Vector3 InsideSphere(float r) => Random.insideUnitSphere * r;
        public static Vector3 OnSphere() => Random.onUnitSphere;
        public static float Sign() => Random.value < 0.5f ? -1f : 1f;

        /// <summary>Random direction inside a cone around <paramref name="dir"/>.</summary>
        public static Vector3 Cone(Vector3 dir, float angleDeg)
        {
            if (angleDeg <= 0f) return dir.normalized;
            var rot = Quaternion.LookRotation(dir);
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Random.value) * Mathf.Tan(angleDeg * Mathf.Deg2Rad);
            var local = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 1f).normalized;
            return rot * local;
        }
    }
}
