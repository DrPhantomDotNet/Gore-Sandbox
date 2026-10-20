using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Non-blood visual effects: sparks, dust, debris, glass, smoke, fire, flashes.</summary>
    public static class FX
    {
        static Transform root;
        static ParticleSystem sparks, dust, chips, glass, smoke, fire, embers, wisps;
        static readonly List<(Light light, float start, float life, float intensity)> flashes = new List<(Light, float, float, float)>();
        static readonly Queue<Light> lightPool = new Queue<Light>();

        public static Transform Root
        {
            get
            {
                if (root == null) Init();
                return root;
            }
        }

        static void Init()
        {
            root = new GameObject("FX").transform;
            Object.DontDestroyOnLoad(root.gameObject);
            sparks = ParticleKit.Create("Sparks", root, Mats.AdditiveWith(TextureFactory.Spark), 1500, 1.2f, ParticleSystemRenderMode.Stretch, 1f, 0.05f);
            ParticleKit.FadeOut(sparks);
            ParticleKit.Collide(sparks, 0.4f, 0.2f, Layers.WorldMask, false, 0.3f);
            dust = ParticleKit.Create("Dust", root, Mats.ParticleWith(TextureFactory.Smoke), 800, -0.02f);
            ParticleKit.FadeOut(dust, 0.8f);
            ParticleKit.SizeOverLife(dust, 0.6f, 1.6f);
            chips = ParticleKit.Create("Chips", root, Mats.Particle, 1200, 1f);
            ParticleKit.Collide(chips, 0.3f, 0.1f, Layers.WorldMask, false);
            ParticleKit.FadeOut(chips);
            glass = ParticleKit.Create("Glass", root, Mats.AdditiveWith(TextureFactory.Star4), 800, 1.2f);
            ParticleKit.Collide(glass, 0.35f, 0.05f, Layers.WorldMask, false);
            ParticleKit.FadeOut(glass);
            smoke = ParticleKit.Create("Smoke", root, Mats.ParticleWith(TextureFactory.Smoke), 600, -0.05f);
            ParticleKit.FadeOut(smoke, 0.7f);
            ParticleKit.SizeOverLife(smoke, 0.5f, 2.2f);
            fire = ParticleKit.Create("Fire", root, Mats.AdditiveWith(TextureFactory.Smoke), 400, -0.3f);
            ParticleKit.FadeOut(fire);
            ParticleKit.SizeOverLife(fire, 0.7f, 1.4f);
            embers = ParticleKit.Create("Embers", root, Mats.Additive, 500, -0.2f);
            ParticleKit.FadeOut(embers);
            wisps = ParticleKit.Create("Wisps", root, Mats.ParticleWith(TextureFactory.Smoke), 600, -0.02f);
            ParticleKit.FadeOut(wisps, 0.35f);
            ParticleKit.SizeOverLife(wisps, 0.4f, 2.5f);
            var updater = root.gameObject.AddComponent<FXUpdater>();
            updater.hideFlags = HideFlags.HideInInspector;
        }

        public static void Clear()
        {
            if (root == null) return;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) { ps.Clear(); ps.Play(); }
        }

        public static void Sparks(Vector3 pos, Vector3 normal, int count = 14, float speed = 7f)
        {
            if (root == null) Init();
            for (int i = 0; i < count; i++)
                ParticleKit.Emit(sparks, pos, Rng.Cone(normal, 60f) * speed * Random.Range(0.4f, 1.3f), Random.Range(0.02f, 0.05f), Random.Range(0.15f, 0.5f), new Color(1f, Random.Range(0.6f, 0.9f), 0.3f));
        }

        public static void Impact(Vector3 pos, Vector3 normal, SurfaceType surface)
        {
            if (root == null) Init();
            Color dustCol;
            switch (surface)
            {
                case SurfaceType.Metal: Sparks(pos, normal, 16, 8f); dustCol = new Color(0.5f, 0.5f, 0.5f, 0.3f); break;
                case SurfaceType.Wood: dustCol = new Color(0.55f, 0.4f, 0.25f, 0.6f); Chips(pos, normal, new Color(0.55f, 0.38f, 0.2f), 10); break;
                case SurfaceType.Dirt: case SurfaceType.Grass: dustCol = new Color(0.45f, 0.36f, 0.25f, 0.8f); Chips(pos, normal, new Color(0.3f, 0.25f, 0.15f), 12); break;
                case SurfaceType.Glass: Glass(pos, normal, 10); dustCol = new Color(1, 1, 1, 0.1f); break;
                case SurfaceType.Flesh: return;
                default: dustCol = new Color(0.62f, 0.6f, 0.57f, 0.7f); Chips(pos, normal, new Color(0.45f, 0.44f, 0.42f), 9); if (Random.value < 0.4f) Sparks(pos, normal, 4, 5f); break;
            }
            for (int i = 0; i < 6; i++)
                ParticleKit.Emit(dust, pos + normal * 0.05f, Rng.Cone(normal, 35f) * Random.Range(0.4f, 2.2f), Random.Range(0.15f, 0.35f), Random.Range(0.6f, 1.3f), dustCol, Random.Range(0, 360f));
        }

        public static void Chips(Vector3 pos, Vector3 normal, Color color, int count)
        {
            if (root == null) Init();
            for (int i = 0; i < count; i++)
                ParticleKit.Emit(chips, pos, Rng.Cone(normal, 55f) * Random.Range(2f, 6f), Random.Range(0.015f, 0.04f), Random.Range(0.6f, 1.6f), color * Random.Range(0.7f, 1.1f));
        }

        public static void Glass(Vector3 pos, Vector3 dir, int count)
        {
            if (root == null) Init();
            for (int i = 0; i < count; i++)
                ParticleKit.Emit(glass, pos + Random.insideUnitSphere * 0.2f, (Rng.Cone(dir, 70f) * Random.Range(1f, 5f)), Random.Range(0.03f, 0.08f), Random.Range(0.8f, 2f), new Color(0.75f, 0.9f, 1f, 0.8f), Random.Range(0, 360f));
        }

        public static void Dust(Vector3 pos, float radius, Color color, int count)
        {
            if (root == null) Init();
            for (int i = 0; i < count; i++)
                ParticleKit.Emit(dust, pos + Random.insideUnitSphere * radius, Random.insideUnitSphere * 0.8f + Vector3.up * 0.4f, Random.Range(0.3f, 0.7f) * (radius + 0.5f), Random.Range(0.8f, 1.6f), color, Random.Range(0, 360f));
        }

        public static void MuzzleSmoke(Vector3 pos, Vector3 dir, float amount = 1f)
        {
            if (root == null) Init();
            int n = Mathf.RoundToInt(3 * amount);
            for (int i = 0; i < n; i++)
                ParticleKit.Emit(wisps, pos, dir * Random.Range(0.3f, 1.2f) + Vector3.up * 0.2f + Random.insideUnitSphere * 0.15f, Random.Range(0.06f, 0.14f), Random.Range(0.8f, 1.6f), new Color(0.8f, 0.8f, 0.8f, 0.5f), Random.Range(0, 360f));
        }

        public static void Explosion(Vector3 pos, float radius)
        {
            if (root == null) Init();
            for (int i = 0; i < 26; i++)
                ParticleKit.Emit(fire, pos + Random.insideUnitSphere * radius * 0.25f, Random.insideUnitSphere * radius * 2f + Vector3.up * 2f, Random.Range(1f, 2.2f) * radius * 0.5f, Random.Range(0.25f, 0.6f), new Color(1f, Random.Range(0.45f, 0.75f), 0.2f), Random.Range(0, 360f));
            for (int i = 0; i < 30; i++)
                ParticleKit.Emit(smoke, pos + Random.insideUnitSphere * radius * 0.4f, Random.insideUnitSphere * radius * 0.8f + Vector3.up * Random.Range(1f, 3f), Random.Range(1.2f, 2.5f) * radius * 0.4f, Random.Range(2.5f, 5f), new Color(0.18f, 0.17f, 0.16f, 0.8f), Random.Range(0, 360f));
            for (int i = 0; i < 40; i++)
                ParticleKit.Emit(embers, pos, Random.onUnitSphere * Random.Range(4f, 14f) + Vector3.up * 3f, Random.Range(0.04f, 0.1f), Random.Range(0.8f, 2.2f), new Color(1f, 0.6f, 0.2f));
            Sparks(pos, Vector3.up, 40, 14f);
            Chips(pos, Vector3.up, new Color(0.2f, 0.18f, 0.15f), 40);
            Flash(pos, new Color(1f, 0.6f, 0.25f), radius * 4f, 8f, 0.45f);
        }

        public static void Smoke(Vector3 pos, Vector3 vel, float size, Color color, float life)
        {
            if (root == null) Init();
            ParticleKit.Emit(smoke, pos, vel, size, life, color, Random.Range(0, 360f));
        }

        public static void Fire(Vector3 pos, Vector3 vel, float size)
        {
            if (root == null) Init();
            ParticleKit.Emit(fire, pos, vel, size, Random.Range(0.15f, 0.35f), new Color(1f, Random.Range(0.5f, 0.8f), 0.25f), Random.Range(0, 360f));
        }

        /// <summary>Brief point-light flash (muzzle, explosion, sparks).</summary>
        public static void Flash(Vector3 pos, Color color, float range, float intensity, float life)
        {
            if (root == null) Init();
            Light l = lightPool.Count > 0 ? lightPool.Dequeue() : null;
            if (l == null)
            {
                var go = new GameObject("Flash");
                go.transform.SetParent(root, false);
                l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
            }
            l.gameObject.SetActive(true);
            l.transform.position = pos;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            flashes.Add((l, Time.time, life, intensity));
        }

        internal static void UpdateFlashes()
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                if (f.light == null) { flashes.RemoveAt(i); continue; }
                float t = (Time.time - f.start) / f.life;
                if (t >= 1f)
                {
                    f.light.gameObject.SetActive(false);
                    lightPool.Enqueue(f.light);
                    flashes.RemoveAt(i);
                }
                else f.light.intensity = f.intensity * (1f - t) * (1f - t);
            }
        }

        sealed class FXUpdater : MonoBehaviour
        {
            void LateUpdate() => UpdateFlashes();
        }
    }
}
