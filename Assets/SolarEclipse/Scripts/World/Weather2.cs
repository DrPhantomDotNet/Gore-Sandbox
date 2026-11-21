using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 atmosphere extras, added to every map by GameManager next to Weather:
    /// lightning that strikes Goredolls standing in the open, wind gusts that push light props (storms, sandstorms),
    /// breath fog in the cold (snow maps and snowy nights), shivering Goredolls in the snow, and the rare
    /// Solar Eclipse event on daytime outdoor maps. World-changing parts only run on the host.
    /// </summary>
    public sealed class WeatherExtras : MonoBehaviour
    {
        public static WeatherExtras Current { get; private set; }
        public static bool EclipseActive => Current != null && Current.eclipseT > 0.05f;

        MapDef def; MapOptions opt;
        ParticleSystem breath;
        float nextGust, nextBreath, nextShiver, eclipseAt = -1f, eclipseStart, eclipseT;
        Color baseSky, baseEq, baseGround, baseFog; float baseSun = -1f;
        static readonly Collider[] Hits = new Collider[64];
        static readonly int PropMask = Layers.Mask(Layers.Props);

        public static void Begin(MapDef def, MapOptions opt, Transform worldRoot)
        {
            if (def == null || worldRoot == null) return;
            var go = new GameObject("Weather Extras");
            go.transform.SetParent(worldRoot, false);
            var w = go.AddComponent<WeatherExtras>();
            w.def = def; w.opt = opt;
            bool outdoorDay = !def.Indoor && opt != null && opt.Time == TimeOfDay.Day && Weather.Supported(def);
            if (outdoorDay && (Random.value < 0.12f || Rules.ForceEclipse)) w.eclipseAt = Time.time + (Rules.ForceEclipse ? 8f : Random.Range(60f, 200f));
        }

        void OnEnable() { Current = this; }
        void OnDisable() { if (Current == this) Current = null; RestoreLight(); Physiology.AmbientCold = 0f; }

        bool Cold => opt != null && (opt.Weather == WeatherKind.Snow || opt.Weather == WeatherKind.Hail) || (def != null && def.Id != null && (def.Id.Contains("snow") || def.Id.Contains("lodge") || def.Id.Contains("arctic")));
        bool Windy => opt != null && (opt.Weather == WeatherKind.Thunderstorm || opt.Weather == WeatherKind.Sandstorm || opt.Weather == WeatherKind.Hail);

        void Update()
        {
            Physiology.AmbientCold = Cold && !def.Indoor ? 1f : 0f; // hypothermia (Goredoll/Physiology.cs)
            var cam = Camera.main;
            if (cam == null) return;
            var cp = cam.transform.position;
            if (Windy && !def.Indoor && Time.time >= nextGust) { nextGust = Time.time + Random.Range(4f, 11f); if (!NetState.IsClient) Gust(cp); }
            if (Cold && !def.Indoor) Breath(cam);
            if (Cold && !NetState.IsClient && Time.time >= nextShiver) { nextShiver = Time.time + 0.15f; Shiver(); }
            if (eclipseAt > 0f) Eclipse();
            if (def.Indoor) Dust(cp);
        }

        // ------------------------------------------------------------------ indoor dust

        ParticleSystem dust; float dustAcc;
        /// <summary>Indoor maps: tiny dust motes drifting in the air around you, catching the light.</summary>
        void Dust(Vector3 cp)
        {
            if (dust == null) dust = ParticleKit.Create("Dust Motes", transform, Mats.Particle, 300, 0f);
            dustAcc += Time.deltaTime * 14f;
            while (dustAcc >= 1f)
            {
                dustAcc -= 1f;
                var pos = cp + new Vector3(Random.Range(-5f, 5f), Random.Range(-1.2f, 2.2f), Random.Range(-5f, 5f));
                ParticleKit.Emit(dust, pos, Random.insideUnitSphere * 0.04f + Vector3.up * 0.01f, Random.Range(0.006f, 0.014f), Random.Range(6f, 10f), new Color(1f, 0.97f, 0.9f, 0.35f));
            }
        }

        // ------------------------------------------------------------------ wind

        void Gust(Vector3 cp)
        {
            var dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
            float power = Random.Range(4f, 9f) * (opt.Weather == WeatherKind.Sandstorm ? 1.4f : 1f);
            int n = Physics.OverlapSphereNonAlloc(cp, 30f, Hits, PropMask, QueryTriggerInteraction.Ignore);
            var done = new HashSet<Rigidbody>();
            for (int i = 0; i < n; i++)
            {
                var rb = Hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic || rb.mass > 12f || !done.Add(rb)) continue;
                rb.AddForce((dir + Vector3.up * 0.15f) * power * Mathf.Lerp(1f, 0.3f, rb.mass / 12f), ForceMode.VelocityChange);
            }
            AudioManager.Play2D("amb_wind", 0.35f, Random.Range(1.1f, 1.3f), AudioBus.Ambience);
        }

        // ------------------------------------------------------------------ cold

        void Breath(Camera cam)
        {
            if (Time.time < nextBreath) return;
            nextBreath = Time.time + Random.Range(2.6f, 3.6f) * (PlayerFeel.Stamina < 0.5f ? 0.45f : 1f);
            if (breath == null) breath = ParticleKit.Create("Breath", transform, Mats.Particle, 400, 0f);
            var p = PlayerController.Instance;
            if (p != null && !p.IsDead && p.Cam != null && !p.Cam.orthographic)
            {
                var f = p.Cam.transform;
                for (int i = 0; i < 6; i++)
                    ParticleKit.Emit(breath, f.position + f.forward * 0.35f - f.up * 0.12f, f.forward * Random.Range(0.4f, 0.8f) + Random.insideUnitSphere * 0.1f + Vector3.up * 0.05f, Random.Range(0.12f, 0.22f), Random.Range(0.8f, 1.3f), new Color(1f, 1f, 1f, 0.22f), Random.Range(0f, 360f));
            }
            foreach (var d in Goredoll.All)
            {
                if (d == null || d.IsDead || !d.Parts.TryGetValue(PartType.Head, out var h) || h == null) continue;
                if ((h.transform.position - cam.transform.position).sqrMagnitude > 400f || Random.value < 0.4f) continue;
                var fw = h.transform.forward;
                for (int i = 0; i < 4; i++)
                    ParticleKit.Emit(breath, h.transform.position + fw * 0.12f, fw * Random.Range(0.3f, 0.6f) + Random.insideUnitSphere * 0.08f, Random.Range(0.1f, 0.18f), Random.Range(0.7f, 1.1f), new Color(1f, 1f, 1f, 0.2f), Random.Range(0f, 360f));
            }
        }

        void Shiver()
        {
            foreach (var d in Goredoll.All)
            {
                if (d == null || d.IsDead || !d.Parts.TryGetValue(PartType.Chest, out var c) || c == null || c.Body == null) continue;
                c.Body.AddTorque(Random.insideUnitSphere * 0.6f, ForceMode.VelocityChange);
            }
        }

        // ------------------------------------------------------------------ lightning strikes

        /// <summary>Called by Weather when a close bolt falls: sometimes it hits a Goredoll standing in the open near you.</summary>
        public static Vector3 StrikePoint(Vector3 cp, Vector3 fallback)
        {
            if (NetState.IsClient || Random.value > 0.35f) return fallback;
            Goredoll best = null; float bestD = 9999f;
            foreach (var d in Goredoll.All)
            {
                if (d == null || d.IsDead) continue;
                var pos = d.Position; float dist = (pos - cp).magnitude;
                if (dist < 6f || dist > 60f || dist > bestD) continue;
                if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.up, 60f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue; // sheltered
                best = d; bestD = dist;
            }
            if (best == null) return fallback;
            var at = best.Position;
            foreach (var bp in best.Parts.Values)
            {
                if (bp == null) continue;
                bp.TakeDamage(new DamageInfo { Amount = 35f, Type = DamageType.Explosion, Point = bp.transform.position, Direction = Vector3.down, Normal = Vector3.up, Weapon = "Lightning" });
                if (bp.Body != null) bp.Body.AddForce(Random.insideUnitSphere * 4f + Vector3.up * 3f, ForceMode.VelocityChange);
            }
            Burning.Apply(best, 6f, null);
            Stats.Add("lightning");
            Decals.Scorch(at, Vector3.up, 1.6f, null);
            CameraFX.Shake(0.25f);
            return at;
        }

        // ------------------------------------------------------------------ eclipse

        void Eclipse()
        {
            if (Time.time < eclipseAt) return;
            if (eclipseStart <= 0f)
            {
                eclipseStart = Time.time;
                baseSky = RenderSettings.ambientSkyColor; baseEq = RenderSettings.ambientEquatorColor; baseGround = RenderSettings.ambientGroundColor; baseFog = RenderSettings.fogColor;
                baseSun = MapLighting.Sun != null ? MapLighting.Sun.intensity : -1f;
                Toasts.Show("SOLAR ECLIPSE", "The sky is going dark...", new Color(1f, 0.55f, 0.3f));
                AudioManager.Play2D("bell", 0.4f, 0.5f, AudioBus.Sfx);
            }
            // 20 s to totality, 25 s of darkness, 20 s back.
            float t = Time.time - eclipseStart;
            float k = t < 20f ? t / 20f : t < 45f ? 1f : t < 65f ? 1f - (t - 45f) / 20f : 0f;
            k = Mathf.SmoothStep(0f, 1f, k);
            eclipseT = k;
            float dim = Mathf.Lerp(1f, 0.08f, k);
            RenderSettings.ambientSkyColor = Color.Lerp(baseSky * dim, new Color(0.05f, 0.03f, 0.08f), k * 0.5f);
            RenderSettings.ambientEquatorColor = baseEq * dim;
            RenderSettings.ambientGroundColor = baseGround * dim;
            RenderSettings.fogColor = Color.Lerp(baseFog, new Color(0.06f, 0.04f, 0.08f), k);
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Eclipse")) RenderSettings.skybox.SetFloat("_Eclipse", k); // the moon covers the sun (SEG_Sky.shader)
            if (MapLighting.Sun != null && baseSun >= 0f) { MapLighting.Sun.intensity = baseSun * dim; MapLighting.Sun.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.35f), k); }
            if (t >= 65f) { RestoreLight(); eclipseAt = -1f; eclipseT = 0f; }
        }

        void RestoreLight()
        {
            if (baseSun < 0f) return;
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Eclipse")) RenderSettings.skybox.SetFloat("_Eclipse", 0f);
            RenderSettings.ambientSkyColor = baseSky; RenderSettings.ambientEquatorColor = baseEq; RenderSettings.ambientGroundColor = baseGround; RenderSettings.fogColor = baseFog;
            if (MapLighting.Sun != null) { MapLighting.Sun.intensity = baseSun; MapLighting.Sun.color = Color.white; }
            baseSun = -1f;
        }
    }
}
