using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Blood particles: entry sprays, exit blowouts with wall splatter, mist, arterial spurts, drips and bursts.
    /// Droplets that hit the world leave decals (and the occasional splat sound).
    /// </summary>
    public static class BloodFX
    {
        static ParticleSystem spray, mist, drops, chunks;
        static Transform root;

        static void Init()
        {
            root = new GameObject("BloodFX").transform;
            Object.DontDestroyOnLoad(root.gameObject);
            spray = ParticleKit.Create("BloodSpray", root, Mats.ParticleWith(TextureFactory.SoftDot), 3000, 1.1f, ParticleSystemRenderMode.Stretch, 1.2f, 0.035f);
            ParticleKit.Collide(spray, 0.05f, 1f, Layers.DecalMask, true, 0.3f);
            ParticleKit.FadeOut(spray);
            spray.gameObject.AddComponent<BloodCollisions>().Init(spray, 0.35f);
            mist = ParticleKit.Create("BloodMist", root, Mats.ParticleWith(TextureFactory.Smoke), 600, 0.15f);
            ParticleKit.FadeOut(mist, 0.75f);
            ParticleKit.SizeOverLife(mist, 0.6f, 1.8f);
            drops = ParticleKit.Create("BloodDrops", root, Mats.ParticleWith(TextureFactory.SoftDot), 2000, 1f, ParticleSystemRenderMode.Stretch, 1f, 0.02f);
            ParticleKit.Collide(drops, 0f, 1f, Layers.DecalMask, true, 0.3f);
            drops.gameObject.AddComponent<BloodCollisions>().Init(drops, 0.12f);
            chunks = ParticleKit.Create("BloodChunks", root, Mats.ParticleWith(TextureFactory.SoftDot), 800, 1.3f);
            ParticleKit.Collide(chunks, 0.15f, 1f, Layers.DecalMask, true, 0.4f);
            chunks.gameObject.AddComponent<BloodCollisions>().Init(chunks, 0.5f);
        }

        static bool Ready()
        {
            if (!Settings.GoreEnabled) return false;
            if (root == null) Init();
            return true;
        }

        static Color Col(float darken = 1f)
        {
            var c = Settings.BloodColor;
            float k = Random.Range(0.75f, 1.1f) * darken;
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        public static void Clear()
        {
            if (root == null) return;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Clear();
        }

        /// <summary>Entry wound: a small backspray toward the shooter.</summary>
        public static void Entry(Vector3 pos, Vector3 normal, Vector3 dir, float amount)
        {
            if (!Ready()) return;
            int n = Mathf.RoundToInt(10 * amount * Settings.BloodScale);
            var back = (normal - dir * 0.3f).normalized;
            for (int i = 0; i < n; i++)
                ParticleKit.Emit(spray, pos, Rng.Cone(back, 35f) * Random.Range(1f, 4f), Random.Range(0.012f, 0.03f), Random.Range(0.4f, 1.2f), Col());
            Mist(pos, back, amount * 0.6f);
        }

        /// <summary>Exit wound: a big forward blowout and mist, plus splatter on whatever is behind.</summary>
        public static void Exit(Vector3 pos, Vector3 dir, float amount)
        {
            if (!Ready()) return;
            int n = Mathf.RoundToInt(28 * amount * Settings.BloodScale);
            for (int i = 0; i < n; i++)
                ParticleKit.Emit(spray, pos, Rng.Cone(dir, 22f) * Random.Range(3f, 9f), Random.Range(0.015f, 0.04f), Random.Range(0.5f, 1.4f), Col());
            Mist(pos, dir, amount);
            if (Physics.Raycast(pos + dir * 0.05f, dir, out var hit, 4f, Layers.DecalMask, QueryTriggerInteraction.Ignore))
            {
                bool wall = Mathf.Abs(hit.normal.y) < 0.5f;
                float size = Mathf.Lerp(1.1f, 0.5f, hit.distance / 4f) * Mathf.Clamp(amount, 0.5f, 2f);
                Decals.Blood(hit.point, hit.normal, size, 0, hit.collider);
                if (wall && Random.value < 0.7f) Decals.Blood(hit.point + Vector3.down * size * 0.3f, hit.normal, size * 0.8f, 1, hit.collider);
            }
        }

        public static void Mist(Vector3 pos, Vector3 dir, float amount)
        {
            if (!Ready()) return;
            int n = Mathf.RoundToInt(6 * amount);
            for (int i = 0; i < n; i++)
                ParticleKit.Emit(mist, pos, dir * Random.Range(0.3f, 1.4f) + Random.insideUnitSphere * 0.3f, Random.Range(0.08f, 0.22f) * Mathf.Sqrt(amount), Random.Range(0.4f, 0.9f), Col(0.9f).WithAlpha(0.55f), Random.Range(0, 360f));
        }

        /// <summary>One arterial spurt (called on each heartbeat).</summary>
        public static void Spurt(Vector3 pos, Vector3 dir, float strength)
        {
            if (!Ready()) return;
            int n = Mathf.RoundToInt(14 * strength * Settings.BloodScale);
            for (int i = 0; i < n; i++)
                ParticleKit.Emit(spray, pos, Rng.Cone(dir, 8f) * Random.Range(2.5f, 4.5f) * strength, Random.Range(0.015f, 0.03f), Random.Range(0.6f, 1.5f), Col());
        }

        public static void Drip(Vector3 pos, Vector3 inheritVelocity)
        {
            if (!Ready()) return;
            ParticleKit.Emit(drops, pos, inheritVelocity + Random.insideUnitSphere * 0.15f, Random.Range(0.01f, 0.02f), 3f, Col(0.9f));
        }

        public static void Burst(Vector3 pos, Vector3 dir, float amount, Color color)
        {
            if (!Ready()) return;
            int n = Mathf.RoundToInt(45 * amount * Settings.BloodScale);
            for (int i = 0; i < n; i++)
                ParticleKit.Emit(spray, pos + Random.insideUnitSphere * 0.08f, (Random.onUnitSphere + dir * 0.5f) * Random.Range(2f, 8f), Random.Range(0.015f, 0.05f), Random.Range(0.5f, 1.5f), new Color(color.r * Random.Range(0.75f, 1.1f), color.g, color.b, 1));
            for (int i = 0; i < n / 4; i++)
                ParticleKit.Emit(chunks, pos, (Random.onUnitSphere + dir * 0.4f) * Random.Range(2f, 6f), Random.Range(0.03f, 0.07f), Random.Range(1f, 2f), color * 0.7f);
            for (int i = 0; i < 6; i++)
                ParticleKit.Emit(mist, pos, Random.insideUnitSphere, Random.Range(0.3f, 0.6f) * amount, Random.Range(0.5f, 1f), color.WithAlpha(0.6f), Random.Range(0, 360f));
        }

        /// <summary>Blood from the mouth (coughing with lung injuries).</summary>
        public static void Mouth(Goredoll d)
        {
            if (!Ready() || !d.Parts.TryGetValue(PartType.Head, out var head) || head.Severed || head.Gibbed) return;
            var t = head.transform;
            var pos = t.TransformPoint(new Vector3(0, HumanModel.HeadCenterY - 0.05f, 0.09f));
            for (int i = 0; i < 12; i++)
                ParticleKit.Emit(spray, pos, Rng.Cone(t.forward, 25f) * Random.Range(1f, 3f), Random.Range(0.01f, 0.025f), Random.Range(0.5f, 1f), Col());
        }

        /// <summary>Spawns decals where particles land.</summary>
        sealed class BloodCollisions : MonoBehaviour
        {
            ParticleSystem ps;
            float chance;
            readonly List<ParticleCollisionEvent> events = new List<ParticleCollisionEvent>(32);
            int frame, thisFrame;
            float lastSound;

            public void Init(ParticleSystem system, float decalChance) { ps = system; chance = decalChance; }

            void OnParticleCollision(GameObject other)
            {
                if (Time.frameCount != frame) { frame = Time.frameCount; thisFrame = 0; }
                int n = ps.GetCollisionEvents(other, events);
                var col = other.GetComponent<Collider>();
                for (int i = 0; i < n && thisFrame < 6; i++)
                {
                    if (Random.value > chance) continue;
                    thisFrame++;
                    var e = events[i];
                    bool wall = Mathf.Abs(e.normal.y) < 0.5f;
                    float size = Random.Range(0.12f, 0.35f) * (chance > 0.4f ? 1.6f : 1f);
                    Decals.Blood(e.intersection, e.normal, size, wall && Random.value < 0.5f ? 1 : chance < 0.2f ? 3 : 0, col, 0f, chance < 0.2f ? 90f : 0f);
                    if (Time.time - lastSound > 0.12f && Random.value < 0.3f)
                    {
                        lastSound = Time.time;
                        AudioManager.Play(chance < 0.2f ? "drip" : "splat", e.intersection, 0.35f, Random.Range(0.9f, 1.15f), 1f, 1f, 18f);
                    }
                }
            }
        }
    }
}
