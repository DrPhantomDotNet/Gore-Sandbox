using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Map weather picked on the map select screen: rain (with splashes and a rain loop), thunderstorms (lightning
    /// flashes and thunder delayed by distance), fog and snow. Purely cosmetic and local: every player runs their own
    /// copy (the host only sends the weather kind in the Welcome message), so nothing here is networked.
    /// Particles follow the camera; each drop is ray-cast down when emitted so it dies on the first surface below
    /// (no rain under roofs), and the rain loop is muffled while something is overhead.
    /// </summary>
    public sealed class Weather : MonoBehaviour
    {
        public static Weather Current { get; private set; }
        public WeatherKind Kind { get; private set; }

        ParticleSystem drops, splashes, flakes;
        AudioSource rainLoop;
        LineRenderer bolt;
        float emitAcc, splashAcc, nextLightning, shelterT, shelterCheck;
        bool sheltered;
        // Lightning flash state
        float flashT = -1f, flashPower;
        Color baseSky, baseEq, baseGround;
        float baseSun;
        int thunderPending;
        readonly float[] thunderAt = new float[4];
        readonly bool[] thunderNear = new bool[4];
        readonly float[] thunderVol = new float[4];

        static readonly int RainMask = Layers.Mask(Layers.Default, Layers.Props);

        /// <summary>Maps where weather makes no sense: indoor maps, the boss realm and the Backrooms.</summary>
        public static bool Supported(MapDef def)
            => def != null && !def.Indoor && def.Id != BossRealm.MapId && def.Id != BackroomsMap.MapId;

        /// <summary>Called by GameManager after the map is built (lighting already applied).</summary>
        public static void Begin(MapDef def, MapOptions opt, Transform worldRoot)
        {
            if (opt == null || opt.Weather == WeatherKind.Clear || !Supported(def) || worldRoot == null) return;
            var go = new GameObject("Weather");
            go.transform.SetParent(worldRoot, false);
            var w = go.AddComponent<Weather>();
            w.Init(opt.Weather, opt.Time);
        }

        void Init(WeatherKind kind, TimeOfDay time)
        {
            Kind = kind;
            Current = this;
            bool wet = kind == WeatherKind.Rain || kind == WeatherKind.Thunderstorm;
            bool night = time == TimeOfDay.Night || time == TimeOfDay.BloodMoon;

            // ---- Look: darker / greyer ambient, denser fog, overcast sky.
            float dim = kind == WeatherKind.Thunderstorm ? 0.55f : kind == WeatherKind.Rain ? 0.68f : kind == WeatherKind.Fog ? 0.8f : 0.92f;
            RenderSettings.ambientSkyColor *= dim;
            RenderSettings.ambientEquatorColor *= dim;
            RenderSettings.ambientGroundColor *= dim;
            var sun = MapLighting.Sun;
            if (sun != null) sun.intensity *= kind == WeatherKind.Snow ? 0.7f : kind == WeatherKind.Fog ? 0.6f : 0.4f;
            var grey = night ? new Color(0.05f, 0.06f, 0.08f) : kind == WeatherKind.Snow ? new Color(0.78f, 0.8f, 0.84f) : new Color(0.42f, 0.45f, 0.5f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, grey, kind == WeatherKind.Fog ? 0.8f : 0.6f);
            float add = kind == WeatherKind.Fog ? 0.04f : kind == WeatherKind.Thunderstorm ? 0.012f : kind == WeatherKind.Snow ? 0.014f : 0.008f;
            RenderSettings.fogDensity += add;
            if (kind == WeatherKind.Fog) RenderSettings.fog = true; // fog weather overrides the fog setting: it's the point
            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_Top"))
            {
                float k = kind == WeatherKind.Fog ? 0.85f : 0.6f;
                sky.SetColor("_Top", Color.Lerp(sky.GetColor("_Top"), RenderSettings.fogColor * 0.8f, k));
                sky.SetColor("_Horizon", Color.Lerp(sky.GetColor("_Horizon"), RenderSettings.fogColor, k));
                if (sky.HasProperty("_Stars")) sky.SetFloat("_Stars", 0f);
                if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0f);
            }
            baseSky = RenderSettings.ambientSkyColor; baseEq = RenderSettings.ambientEquatorColor; baseGround = RenderSettings.ambientGroundColor;
            baseSun = sun != null ? sun.intensity : 0f;

            // ---- Particles
            if (wet)
            {
                drops = ParticleKit.Create("Rain", transform, Mats.Particle, 6000, 0f, ParticleSystemRenderMode.Stretch, 1f, 0.045f);
                splashes = ParticleKit.Create("RainSplash", transform, Mats.Particle, 1500, 1.2f);
                ParticleKit.FadeOut(splashes, 0.8f);
                ParticleKit.SizeOverLife(splashes, 1f, 0.3f);
                rainLoop = AudioManager.CreateLoop(kind == WeatherKind.Thunderstorm ? "amb_rain_heavy" : "amb_rain", transform, 0.75f, AudioBus.Ambience, 50f);
                if (rainLoop != null) rainLoop.spatialBlend = 0f;
            }
            else if (kind == WeatherKind.Snow)
            {
                flakes = ParticleKit.Create("Snow", transform, Mats.Particle, 5000, 0f);
                ParticleKit.FadeOut(flakes, 1f);
            }
            if (kind == WeatherKind.Thunderstorm)
            {
                nextLightning = Time.time + Random.Range(3f, 8f);
                var bgo = new GameObject("Lightning Bolt");
                bgo.transform.SetParent(transform, false);
                bolt = bgo.AddComponent<LineRenderer>();
                bolt.sharedMaterial = Mats.Lines;
                bolt.useWorldSpace = true;
                bolt.widthMultiplier = 3f;
                bolt.startColor = bolt.endColor = new Color(0.85f, 0.9f, 1f, 1f);
                bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bolt.enabled = false;
            }
        }

        void OnDestroy() { if (Current == this) Current = null; }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var cp = cam.transform.position;
            float dt = Time.deltaTime;

            // Shelter check (something solid overhead within 30 m) for the rain loop volume, twice a second.
            if ((shelterCheck -= Time.unscaledDeltaTime) <= 0f)
            {
                shelterCheck = 0.5f;
                sheltered = Physics.Raycast(cp + Vector3.up * 0.3f, Vector3.up, 30f, RainMask, QueryTriggerInteraction.Ignore);
            }
            shelterT = Mathf.MoveTowards(shelterT, sheltered ? 1f : 0f, Time.unscaledDeltaTime * 1.5f);
            if (rainLoop != null) AudioManager.ApplyBus(rainLoop, AudioBus.Ambience, Mathf.Lerp(0.75f, 0.3f, shelterT));

            if (drops != null) EmitRain(cp, dt);
            if (flakes != null) EmitSnow(cp, dt);
            if (Kind == WeatherKind.Thunderstorm) UpdateLightning(cp);
        }

        // ------------------------------------------------------------------ rain & snow

        void EmitRain(Vector3 cp, float dt)
        {
            bool heavy = Kind == WeatherKind.Thunderstorm;
            float rate = (heavy ? 1500f : 900f) * (Application.isMobilePlatform ? 0.5f : 1f);
            emitAcc += rate * dt;
            int n = Mathf.Min(120, (int)emitAcc);
            emitAcc -= n;
            var wind = heavy ? new Vector3(2.5f, 0, 1.2f) : new Vector3(0.8f, 0, 0.4f);
            const float speed = 19f;
            var col = new Color(0.75f, 0.8f, 0.9f, heavy ? 0.45f : 0.38f);
            for (int i = 0; i < n; i++)
            {
                var off = Random.insideUnitCircle * 20f;
                var start = new Vector3(cp.x + off.x, cp.y + 14f, cp.z + off.y);
                var vel = new Vector3(wind.x, -speed, wind.z);
                float life = 2.2f;
                if (Physics.Raycast(start, vel.normalized, out var hit, 60f, RainMask, QueryTriggerInteraction.Ignore))
                {
                    life = hit.distance / vel.magnitude;
                    // Splash for a fraction of the drops close to the player (timing doesn't matter: it's random rain).
                    if ((splashAcc += heavy ? 0.45f : 0.35f) >= 1f && (hit.point - cp).sqrMagnitude < 15f * 15f)
                    {
                        splashAcc -= 1f;
                        for (int k = 0; k < 2; k++)
                            ParticleKit.Emit(splashes, hit.point + hit.normal * 0.03f, new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.8f, 1.6f), Random.Range(-0.6f, 0.6f)),
                                Random.Range(0.025f, 0.05f), Random.Range(0.15f, 0.3f), new Color(0.8f, 0.85f, 0.95f, 0.6f));
                    }
                }
                if (life < 0.03f) continue;
                ParticleKit.Emit(drops, start, vel, 0.025f, life, col);
            }
        }

        void EmitSnow(Vector3 cp, float dt)
        {
            float rate = 420f * (Application.isMobilePlatform ? 0.5f : 1f);
            emitAcc += rate * dt;
            int n = Mathf.Min(60, (int)emitAcc);
            emitAcc -= n;
            for (int i = 0; i < n; i++)
            {
                var off = Random.insideUnitCircle * 22f;
                var start = new Vector3(cp.x + off.x, cp.y + 10f, cp.z + off.y);
                var vel = new Vector3(Random.Range(-0.4f, 0.6f), -Random.Range(1.1f, 1.8f), Random.Range(-0.4f, 0.4f));
                float life = 12f;
                if (Physics.Raycast(start, vel.normalized, out var hit, 30f, RainMask, QueryTriggerInteraction.Ignore))
                    life = hit.distance / vel.magnitude;
                if (life < 0.1f) continue;
                ParticleKit.Emit(flakes, start, vel, Random.Range(0.04f, 0.09f), life, new Color(1f, 1f, 1f, 0.9f), Random.Range(0f, 360f));
            }
        }

        // ------------------------------------------------------------------ lightning & thunder

        void UpdateLightning(Vector3 cp)
        {
            float now = Time.time;
            if (now >= nextLightning)
            {
                nextLightning = now + Random.Range(7f, 22f);
                float dist = Random.Range(120f, 2200f);
                bool near = dist < 600f;
                flashT = 0f;
                flashPower = Mathf.Lerp(1.6f, 0.35f, Mathf.InverseLerp(120f, 2200f, dist));
                if (near) CameraFX.Flash(new Color(0.85f, 0.9f, 1f), 0.18f * flashPower);
                var dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
                if (near && bolt != null) DrawBolt(cp + dir * Mathf.Min(dist, 350f));
                // Thunder arrives later (speed of sound ~343 m/s), quieter the further away.
                if (thunderPending < thunderAt.Length)
                {
                    thunderAt[thunderPending] = now + dist / 343f;
                    thunderNear[thunderPending] = dist < 500f;
                    thunderVol[thunderPending] = Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(120f, 2200f, dist));
                    thunderPending++;
                }
            }
            for (int i = thunderPending - 1; i >= 0; i--)
            {
                if (now < thunderAt[i]) continue;
                AudioManager.Play2D(thunderNear[i] ? "thunder_near" : "thunder_far", thunderVol[i], Random.Range(0.9f, 1.05f), AudioBus.Ambience);
                if (thunderNear[i]) CameraFX.Shake(0.12f);
                thunderPending--;
                thunderAt[i] = thunderAt[thunderPending]; thunderNear[i] = thunderNear[thunderPending]; thunderVol[i] = thunderVol[thunderPending];
            }

            // Flash: two or three quick flickers, then back to the stored (dimmed) lighting.
            if (flashT >= 0f)
            {
                flashT += Time.unscaledDeltaTime;
                float f = 0f;
                if (flashT < 0.08f) f = 1f;
                else if (flashT < 0.14f) f = 0.2f;
                else if (flashT < 0.24f) f = 0.85f;
                else if (flashT < 0.6f) f = 1f - (flashT - 0.24f) / 0.36f;
                else { f = 0f; flashT = -1f; if (bolt != null) bolt.enabled = false; }
                f = Mathf.Max(0f, f) * flashPower;
                var flashCol = new Color(0.55f, 0.6f, 0.75f) * f;
                RenderSettings.ambientSkyColor = baseSky + flashCol;
                RenderSettings.ambientEquatorColor = baseEq + flashCol * 0.7f;
                RenderSettings.ambientGroundColor = baseGround + flashCol * 0.4f;
                if (MapLighting.Sun != null) MapLighting.Sun.intensity = baseSun + f * 0.6f;
                if (bolt != null && bolt.enabled) bolt.widthMultiplier = 3f * Mathf.Clamp01(f * 1.5f);
            }
        }

        void DrawBolt(Vector3 ground)
        {
            const int segs = 14;
            bolt.positionCount = segs + 1;
            var top = ground + new Vector3(Random.Range(-40f, 40f), 220f, Random.Range(-40f, 40f));
            var gp = new Vector3(ground.x, Mathf.Min(ground.y, 0f), ground.z);
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                var p = Vector3.Lerp(top, gp, t);
                if (i > 0 && i < segs) p += new Vector3(Random.Range(-14f, 14f), Random.Range(-4f, 4f), Random.Range(-14f, 14f));
                bolt.SetPosition(i, p);
            }
            bolt.enabled = true;
        }
    }
}
