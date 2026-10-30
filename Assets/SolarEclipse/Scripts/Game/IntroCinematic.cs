using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SolarEclipse.Audio;

namespace SolarEclipse
{
    /// <summary>
    /// The 3D half of the startup cinematic (UI/Screens/IntroScreen.cs drives the clock and draws the overlays).
    /// "The Subject": a rain-soaked alley at night, a Goredoll under a flickering street lamp, a heartbeat, a laser
    /// dot creeping up his chest, dead silence, one rifle shot in slow motion, the logo slam over his body, then the
    /// camera cranes down into the live main-menu backdrop so the menu takes over without a cut.
    /// Built far away from the menu backdrop (which stays alive underneath); everything it changes (cameras,
    /// listeners, sun, fog, ambient light, slow motion, kill feed) is restored by SwapToMenu / Dispose.
    /// Phone budget: ~15 boxes, 2 point lights, no shadows, one 700-particle rain system, one Goredoll.
    /// </summary>
    public sealed class IntroCinematic : MonoBehaviour
    {
        // Timeline (seconds) - shared with the score in Audio/RecipesIntro.cs.
        public const float ShotTime = RecipesIntro.ShotTime, SilenceTime = RecipesIntro.SilenceTime, BlackTime = RecipesIntro.BlackTime,
            LogoTime = RecipesIntro.LogoTime, SwapTime = RecipesIntro.SwapTime, EndTime = RecipesIntro.EndTime;
        const float LaserTime = 8.8f, TurnTime = 8.1f;

        static readonly Vector3 Origin = new Vector3(0f, 0f, 400f);
        static Vector3 L(float x, float y, float z) => Origin + new Vector3(x, y, z);
        static readonly Vector3 Shooter = L(-1.6f, 2.7f, -17f);
        static readonly float[] Flickers = { 2.2f, 2.34f, 5.7f, 9.35f, 9.5f, 9.62f, 9.8f };
        static readonly Color FogColor = new Color(0.015f, 0.02f, 0.035f);

        Transform set;
        Camera cam;
        Light lamp, rim;
        Goredoll doll;
        ParticleSystem rain;
        Transform laser, dot;
        Mesh dotMesh;
        AudioSource music, sfx, heart;
        float rainAcc, nextBeat = 1.0f, lampFlash, dollYaw;
        Vector3 lastDot, smoothLook, logoFocus;
        int phase = -1;
        bool shotFired;

        /// <summary>White screen flash 0..1 (drawn by IntroScreen).</summary>
        public float Flash { get; private set; }
        public float Shake { get; private set; }
        public bool Swapped { get; private set; }

        // Saved state
        readonly List<Behaviour> disabled = new List<Behaviour>();
        bool savedFog, sunWasOn, killFeedSaved, savedKillFeed, disposed;
        FogMode savedFogMode;
        Color savedFogColor, savedAmbLight, savedAmbSky, savedAmbEq, savedAmbGround;
        float savedFogDensity;
        AmbientMode savedAmbMode;
        Vector3 flyFromPos;
        Quaternion flyFromRot;

        public static IntroCinematic Create()
        {
            var go = new GameObject("IntroCinematic");
            var c = go.AddComponent<IntroCinematic>();
            c.Build();
            return c;
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            // Take over the screen and the ears: the menu backdrop keeps running but nobody looks at it.
            foreach (var c in Camera.allCameras) if (c != null && c.enabled) { c.enabled = false; disabled.Add(c); }
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l != null && l.enabled) { l.enabled = false; disabled.Add(l); }
            sunWasOn = MapLighting.Sun != null && MapLighting.Sun.enabled;
            if (sunWasOn) MapLighting.Sun.enabled = false;

            savedFog = RenderSettings.fog; savedFogMode = RenderSettings.fogMode; savedFogColor = RenderSettings.fogColor; savedFogDensity = RenderSettings.fogDensity;
            savedAmbMode = RenderSettings.ambientMode; savedAmbLight = RenderSettings.ambientLight;
            savedAmbSky = RenderSettings.ambientSkyColor; savedAmbEq = RenderSettings.ambientEquatorColor; savedAmbGround = RenderSettings.ambientGroundColor;
            float br = Settings.Data.brightness;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.065f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.075f, 0.085f, 0.12f) * br;

            set = new GameObject("IntroSet").transform;
            set.SetParent(transform, false);
            var b = new MapBuilder(set, null, new MapOptions());
            var brick = MapBuilder.Tex(TextureFactory.Brick);
            // Wet asphalt: built as "metal" for the glossy sheen, but it is concrete for impacts and footsteps.
            var ground = b.Box("Wet Asphalt", L(0, -0.25f, 4f), Quaternion.identity, new Vector3(9.2f, 0.5f, 42f), MapBuilder.Tex(TextureFactory.Asphalt), SurfaceType.Metal, new Color(0.33f, 0.34f, 0.38f));
            var surf = ground.GetComponent<Surface>(); if (surf != null) surf.Type = SurfaceType.Concrete;
            b.Block("Wall L", L(-4.6f, 0, -17f), L(-4.1f, 7.5f, 25f), brick, SurfaceType.Concrete, new Color(0.36f, 0.3f, 0.3f));
            b.Block("Wall R", L(4.1f, 0, -17f), L(4.6f, 7.5f, 25f), brick, SurfaceType.Concrete, new Color(0.36f, 0.3f, 0.3f));
            b.Block("Back Wall", L(-4.6f, 0, 15f), L(4.6f, 8f, 15.6f), brick, SurfaceType.Concrete, new Color(0.3f, 0.26f, 0.26f));
            b.Box("Dumpster", L(-3.2f, 0.65f, 9.5f), Quaternion.Euler(0, 4, 0), new Vector3(1.6f, 1.3f, 1.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.12f, 0.22f, 0.16f));
            b.Box("Crate", L(3.3f, 0.4f, 1.6f), Quaternion.Euler(0, 25, 0), Vector3.one * 0.8f, MapBuilder.Tex(TextureFactory.Wood), SurfaceType.Wood);
            b.Box("Crate2", L(3.0f, 0.3f, 2.7f), Quaternion.Euler(0, -12, 0), Vector3.one * 0.6f, MapBuilder.Tex(TextureFactory.Wood), SurfaceType.Wood);
            b.Cylinder("Barrel", L(-3.4f, 0, 3.4f), 0.32f, 0.95f, Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.27f, 0.3f), 12);
            b.Box("Pipe", L(4.0f, 3.6f, 4f), Quaternion.identity, new Vector3(0.12f, 0.12f, 30f), Mats.LitMetal, SurfaceType.Metal, new Color(0.2f, 0.2f, 0.22f), 0.5f, false);
            b.Box("Neon", L(-4.05f, 3.2f, 11f), Quaternion.identity, new Vector3(0.06f, 0.35f, 1.8f), Mats.Glow(new Color(0.15f, 0.5f, 1f), 2.5f), SurfaceType.Glass, null, 0.5f, false);
            b.Box("Door Light", L(4.05f, 2.4f, 12.5f), Quaternion.identity, new Vector3(0.06f, 0.12f, 0.5f), Mats.Glow(new Color(1f, 0.25f, 0.15f), 2f), SurfaceType.Glass, null, 0.5f, false);
            // Arm points along -x, so the head (and its light) hangs right above the Goredoll.
            lamp = b.StreetLamp(L(1.3f, 0, 6.6f), -90f, new Color(1f, 0.72f, 0.45f), 5f, 10f, 2.6f);
            b.Finish();

            var rimGo = new GameObject("Rim Light");
            rimGo.transform.SetParent(set, false);
            rimGo.transform.position = L(0.5f, 2.4f, 10f);
            rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Point; rim.color = new Color(1f, 0.12f, 0.08f); rim.range = 8f; rim.intensity = 1.8f; rim.shadows = LightShadows.None;
            Physics.SyncTransforms();

            doll = GoredollFactory.Spawn(L(0, 0, 6f), 0f, "Passive", -1, set);
            if (doll != null)
            {
                if (doll.Brain != null) doll.Brain.enabled = false;
                doll.DisplayName = "The Subject";
            }

            var camGo = new GameObject("IntroCamera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cam.fieldOfView = 48f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 70f;
            cam.depth = 10f;
            cam.cullingMask = ~((1 << Layers.Viewmodel) | (1 << 31));
            camGo.AddComponent<AudioListener>();

            rain = ParticleKit.Create("IntroRain", transform, Mats.ParticleWith(TextureFactory.SoftDot), 700, 0.5f, ParticleSystemRenderMode.Stretch, 1f, 0.03f);

            // Laser sight: a thin emissive beam and an additive dot (vertex-coloured quad, shared material untouched).
            var mb = new MeshBuilder { Color = Color.white, Gloss = 0f };
            mb.Box(Vector3.zero, Vector3.one);
            laser = MakeRenderer("Laser", mb.ToMesh("IntroLaser"), Mats.Glow(new Color(1f, 0.05f, 0.04f), 5f)).transform;
            dotMesh = new Mesh { name = "IntroLaserDot" };
            dotMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            dotMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var red = new Color(1f, 0.1f, 0.08f, 1f);
            dotMesh.colors = new[] { red, red, red, red };
            dotMesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 }; // both sides
            dotMesh.RecalculateBounds();
            dot = MakeRenderer("LaserDot", dotMesh, Mats.AdditiveWith(TextureFactory.Glow)).transform;
            laser.gameObject.SetActive(false);
            dot.gameObject.SetActive(false);

            music = MakeSource(); sfx = MakeSource(); heart = MakeSource();
            var score = SoundBank.Get("intro_score");
            if (score != null) { music.clip = score; music.volume = Settings.Data.musicVolume; music.Play(); }
            Step(0f, -0.001f, 0f);
        }

        GameObject MakeRenderer(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(set, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f; s.dopplerLevel = 0f;
            return s;
        }

        void OneShot(AudioSource src, string name, float volume, float pitch = 1f)
        {
            var clip = SoundBank.Get(name);
            if (clip == null || src == null) return;
            src.pitch = pitch;
            src.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------ timeline

        static float S(float x) { x = Mathf.Clamp01(x); return x * x * x * (x * (x * 6f - 15f) + 10f); } // smootherstep
        static float Ramp(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

        /// <summary>Advances the cinematic to time <paramref name="t"/> (seconds, real time). Events between prevT and t fire once.</summary>
        public void Step(float t, float prevT, float dt)
        {
            if (disposed) return;
            bool At(float m) => prevT < m && t >= m;
            float sfxVol = Settings.Data.sfxVolume;
            Flash *= Mathf.Exp(-dt * 5f);
            Shake *= Mathf.Exp(-dt * 2.5f);
            lampFlash *= Mathf.Exp(-dt * 6f);

            if (!Swapped)
            {
                if (music != null) music.volume = Settings.Data.musicVolume;
                DriveDoll(t);

                // Lamp: subtle wobble, scripted dropouts with a buzz, a pop on the shot, blood-red under the logo.
                if (lamp != null)
                {
                    bool off = false;
                    foreach (var f in Flickers) if (t >= f && t < f + 0.07f) off = true;
                    foreach (var f in Flickers) if (At(f)) OneShot(sfx, "intro_buzz", 0.35f * sfxVol, Random.Range(0.9f, 1.1f));
                    float k = 0.9f + 0.1f * Mathf.PerlinNoise(t * 7f, 0.5f);
                    lamp.intensity = off ? 0.12f : 2.6f * k + lampFlash * 6f;
                    if (t >= BlackTime) { lamp.color = new Color(1f, 0.16f, 0.1f); lamp.intensity = 2.4f; }
                }
                if (rim != null) rim.intensity = t >= BlackTime ? 0.6f : 1.8f;

                // Heartbeat: speeds up toward the silence, then two last, slower beats after the shot.
                if (t >= nextBeat && t < SilenceTime - 0.25f)
                {
                    float x = Mathf.Clamp01(t / 10.4f);
                    OneShot(heart, "heartbeat", Mathf.Lerp(0.35f, 0.95f, x) * sfxVol, 1f);
                    nextBeat += Mathf.Lerp(1.15f, 0.42f, x * x);
                }
                if (At(ShotTime + 1.3f)) OneShot(heart, "heartbeat", 0.6f * sfxVol, 0.85f);
                if (At(ShotTime + 2.8f)) OneShot(heart, "heartbeat", 0.35f * sfxVol, 0.75f);

                UpdateLaser(t);
                if (At(ShotTime)) FireShot();
                // Re-assert the slow motion after the death frame ("slow-mo on kill" would shorten it).
                if (At(ShotTime + 0.1f) && GameManager.I != null) GameManager.I.SlowMo(0.2f, 2.5f);
                if (At(ShotTime + 1.9f)) BloodPool(1.5f, 0);
                if (At(ShotTime + 2.6f)) BloodPool(0.9f, 2);
                if (At(BlackTime)) logoFocus = doll != null ? doll.Position : L(0, 0.3f, 8f);
                EmitRain(t);
                if (At(SwapTime)) SwapToMenu();
            }
            UpdateCamera(t, dt);
        }

        void DriveDoll(float t)
        {
            if (doll == null || shotFired || cam == null) return;
            var rag = doll.Ragdoll;
            if (rag == null) return;
            rag.DesiredVelocity = Vector3.zero;
            rag.Pose = PoseMode.Idle;
            dollYaw = t < TurnTime ? 0f : 180f;
            rag.DesiredYaw = dollYaw;
            // Head hangs low at first, then he looks straight into the lens.
            var look = t < TurnTime + 0.3f ? L(0, 0.9f, 9f) : cam.transform.position;
            rag.LookTarget = look;
            if (doll.Face != null)
            {
                doll.Face.LookAt(look);
                doll.Face.Expression = t > LaserTime + 0.4f ? Expression.Afraid : Expression.Neutral;
            }
        }

        bool TryChest(out BodyPart chest)
        {
            chest = null;
            return doll != null && !doll.IsDead && doll.Parts.TryGetValue(PartType.Chest, out chest) && chest != null && chest.Body != null;
        }

        void UpdateLaser(float t)
        {
            bool on = t >= LaserTime && t < ShotTime && TryChest(out _);
            if (laser.gameObject.activeSelf != on) laser.gameObject.SetActive(on);
            if (dot.gameObject.activeSelf != on) dot.gameObject.SetActive(on);
            if (!on) return;
            TryChest(out var chest);
            var center = chest.Body.worldCenterOfMass;
            // The dot drifts in from the shoulder and settles over the heart.
            float settle = 1f - S((t - LaserTime) / 1.5f);
            var wander = (Vector3.right * 0.22f + Vector3.up * 0.18f) * settle
                         + new Vector3(Mathf.PerlinNoise(t * 3f, 1.7f) - 0.5f, Mathf.PerlinNoise(2.3f, t * 3f) - 0.5f, 0f) * 0.03f;
            var aim = center + wander;
            var dir = (aim - Shooter).normalized;
            Vector3 p;
            if (Physics.Raycast(Shooter, dir, out var hit, 40f, 1 << Layers.Ragdoll, QueryTriggerInteraction.Ignore)) p = hit.point + hit.normal * 0.004f;
            else p = aim - dir * 0.13f;
            lastDot = p;
            float grow = S((t - LaserTime) / 0.35f);
            float len = Vector3.Distance(Shooter, p);
            laser.position = (Shooter + p) * 0.5f;
            laser.rotation = Quaternion.LookRotation(p - Shooter);
            laser.localScale = new Vector3(0.008f * grow, 0.008f * grow, len);
            dot.position = p;
            dot.rotation = Quaternion.LookRotation(p - cam.transform.position);
            float size = (0.055f + 0.012f * Mathf.Sin(t * 20f)) * grow;
            dot.localScale = new Vector3(size, size, size);
        }

        void FireShot()
        {
            shotFired = true;
            laser.gameObject.SetActive(false);
            dot.gameObject.SetActive(false);
            OneShot(sfx, "intro_shot", Settings.Data.sfxVolume);
            Shake = 1f; Flash = 0.5f; lampFlash = 1f;
            if (GameManager.I != null) GameManager.I.SlowMo(0.2f, 2.6f);
            if (!TryChest(out var chest)) return;
            if (lastDot == Vector3.zero) lastDot = chest.Body.worldCenterOfMass + (Shooter - chest.Body.worldCenterOfMass).normalized * 0.13f;
            // The death would put "The Subject - Shot" in the kill feed over the film: mute it until we are done.
            if (!killFeedSaved) { savedKillFeed = Settings.Data.killFeed; killFeedSaved = true; Settings.Data.killFeed = false; }
            var d = (lastDot - Shooter).normalized;
            chest.TakeDamage(new DamageInfo
            {
                Amount = 260f, Force = d * 45f, Point = lastDot, Normal = -d, Direction = d,
                Type = DamageType.Bullet, Weapon = "Rifle", Caliber = 7.62f, Penetration = 1f,
            });
            BloodFX.Exit(lastDot + d * 0.3f, d, 1.6f);
            if (doll != null && !doll.IsDead) doll.Kill("Shot through the heart");
            if (doll != null) doll.Launch(d * 4.2f + Vector3.up * 1.4f);
        }

        void BloodPool(float size, int kind)
        {
            if (doll == null || !Settings.GoreEnabled || !TryPart(PartType.Chest, out var chest)) return;
            if (Physics.Raycast(chest.transform.position + Vector3.up * 0.5f, Vector3.down, out var hit, 3f, Layers.DecalMask, QueryTriggerInteraction.Ignore))
                Decals.Blood(hit.point, hit.normal, size, kind, hit.collider, 3.5f);
        }

        bool TryPart(PartType type, out BodyPart part)
        {
            part = null;
            return doll != null && doll.Parts.TryGetValue(type, out part) && part != null;
        }

        void EmitRain(float t)
        {
            if (rain == null || cam == null) return;
            rainAcc += Time.deltaTime * 380f; // scaled time: the rain slows down with the slow motion
            int n = Mathf.Min(20, (int)rainAcc);
            rainAcc -= n;
            var ct = cam.transform;
            for (int i = 0; i < n; i++)
            {
                var p = ct.position + ct.forward * Random.Range(0.6f, 9f) + ct.right * Random.Range(-4.5f, 4.5f) + Vector3.up * Random.Range(-0.5f, 4.5f);
                ParticleKit.Emit(rain, p, new Vector3(0.8f, -11f, 0.3f), 0.012f, 0.75f, new Color(0.75f, 0.8f, 0.9f, 0.35f));
            }
        }

        // ------------------------------------------------------------------ camera

        void UpdateCamera(float t, float dt)
        {
            if (cam == null) return;
            Vector3 pos, look; float fov, roll = 0f;
            int ph;
            if (Swapped) { FlyIn(t); return; }
            if (t < 4f)
            {
                ph = 0;
                float u = S(t / 4f);
                pos = Vector3.Lerp(L(-0.7f, 0.32f, -9.5f), L(-0.25f, 0.8f, -3.2f), u);
                look = Vector3.Lerp(L(0, 1.0f, 6f), L(0, 1.45f, 6f), u);
                fov = 48f; roll = Mathf.Lerp(3f, 0f, u);
            }
            else if (t < 8f)
            {
                ph = 1;
                float u = S((t - 4f) / 4f);
                float a = Mathf.Lerp(222f, 138f, u) * Mathf.Deg2Rad, r = Mathf.Lerp(3.6f, 2.5f, u);
                pos = L(Mathf.Sin(a) * r, Mathf.Lerp(1.95f, 1.55f, u), 6f + Mathf.Cos(a) * r);
                look = L(0, 1.5f, 6f);
                fov = 40f; roll = Mathf.Lerp(-2f, 1f, u);
            }
            else if (t < ShotTime)
            {
                ph = 2;
                float u = Mathf.Clamp01((t - 8f) / (ShotTime - 8f));
                pos = Vector3.Lerp(L(0.75f, 1.62f, 4.3f), L(0.42f, 1.58f, 4.85f), 1f - (1f - u) * (1f - u));
                look = TryPart(PartType.Head, out var head) ? head.transform.TransformPoint(new Vector3(0, HumanModel.HeadCenterY, 0)) : L(0, 1.6f, 6f);
                fov = Mathf.Lerp(36f, 28f, u);
            }
            else if (t < BlackTime)
            {
                ph = 3;
                float u = S((t - ShotTime) / (BlackTime - ShotTime));
                pos = Vector3.Lerp(L(3.1f, 1.1f, 3.9f), L(2.7f, 0.75f, 5.6f), u);
                look = doll != null ? doll.Position : L(0, 0.5f, 8f);
                fov = Mathf.Lerp(42f, 38f, u); roll = Mathf.Lerp(4f, 1f, u);
            }
            else
            {
                ph = 4;
                if (logoFocus == Vector3.zero) logoFocus = doll != null ? doll.Position : L(0, 0.3f, 8f);
                float ang = (t - BlackTime) * 8f;
                pos = logoFocus + Quaternion.Euler(0, ang, 0) * new Vector3(0f, 6f, -1.6f);
                look = logoFocus;
                fov = Mathf.Lerp(40f, 34f, Ramp(t, BlackTime, SwapTime));
            }
            // Live targets (head, body) are smoothed; every new shot is a clean cut.
            if (ph != phase) { phase = ph; smoothLook = look; }
            else smoothLook = ph >= 2 ? MathX.Damp(smoothLook, look, 6f, dt) : look;
            var fwd = smoothLook - pos;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            float sh = Shake * Settings.Data.screenShake;
            var shake = Quaternion.Euler((Mathf.PerlinNoise(t * 23f, 0.1f) - 0.5f) * 5f * sh, (Mathf.PerlinNoise(0.7f, t * 23f) - 0.5f) * 5f * sh, 0f);
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd) * Quaternion.Euler(0, 0, roll) * shake);
            cam.fieldOfView = fov;
        }

        void MenuPose(out Vector3 pos, out Quaternion rot, out float fov)
        {
            var mc = MenuBackdrop.Cam;
            if (mc != null) { pos = mc.transform.position; rot = mc.transform.rotation; fov = mc.fieldOfView; return; }
            pos = new Vector3(2.0f, 1.5f, 5.1f);
            rot = Quaternion.LookRotation(new Vector3(0.6f, 1.05f, 1.2f) - pos);
            fov = 45f;
        }

        void FlyIn(float t)
        {
            MenuPose(out var tp, out var tr, out var tfov);
            float e = S((t - SwapTime) / (EndTime - 0.1f - SwapTime));
            cam.transform.SetPositionAndRotation(Vector3.Lerp(flyFromPos, tp, e), Quaternion.Slerp(flyFromRot, tr, e));
            cam.fieldOfView = Mathf.Lerp(38f, tfov, e);
        }

        // ------------------------------------------------------------------ hand-off and cleanup

        /// <summary>Skip: silence the film (the screen then jumps to the swap).</summary>
        public void StopAudio()
        {
            if (music != null) music.Stop();
            if (sfx != null) sfx.Stop();
            if (heart != null) heart.Stop();
        }

        /// <summary>Tears down the alley and restores the menu's world; the camera then cranes down into the menu pose.</summary>
        public void SwapToMenu()
        {
            if (Swapped) return;
            Swapped = true;
            RestoreWorld();
            if (cam != null)
            {
                var mc = MenuBackdrop.Cam;
                cam.clearFlags = mc != null ? mc.clearFlags : CameraClearFlags.Skybox;
                cam.backgroundColor = mc != null ? mc.backgroundColor : Color.black;
                cam.farClipPlane = mc != null ? mc.farClipPlane : 1000f;
                cam.cullingMask = mc != null ? mc.cullingMask : cam.cullingMask;
                MenuPose(out var tp, out var tr, out _);
                // Start high above and to the side of the menu shot (the backdrop's wall is right behind it).
                flyFromPos = tp + Vector3.up * 4.8f + (tr * Vector3.right) * 1.6f;
                var focus = tp + (tr * Vector3.forward) * 4.2f;
                flyFromRot = Quaternion.LookRotation(focus - flyFromPos);
                cam.transform.SetPositionAndRotation(flyFromPos, flyFromRot);
            }
            OneShot(sfx, "logo_whoosh", 0.45f * Settings.Data.sfxVolume, 0.8f);
        }

        void RestoreWorld()
        {
            if (GameManager.I != null) GameManager.I.SlowMo(1f, 0f); // ends any slow motion right away
            if (doll != null) { doll.Despawn(); doll = null; }
            BloodFX.Clear();
            if (set != null) { Destroy(set.gameObject); set = null; laser = null; dot = null; lamp = null; rim = null; }
            if (rain != null) { Destroy(rain.gameObject); rain = null; }
            if (dotMesh != null) { Destroy(dotMesh); dotMesh = null; }
            RenderSettings.fog = savedFog; RenderSettings.fogMode = savedFogMode; RenderSettings.fogColor = savedFogColor; RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.ambientMode = savedAmbMode; RenderSettings.ambientLight = savedAmbLight;
            RenderSettings.ambientSkyColor = savedAmbSky; RenderSettings.ambientEquatorColor = savedAmbEq; RenderSettings.ambientGroundColor = savedAmbGround;
            if (sunWasOn && MapLighting.Sun != null) MapLighting.Sun.enabled = true;
            if (killFeedSaved) { Settings.Data.killFeed = savedKillFeed; killFeedSaved = false; }
        }

        /// <summary>Gives the screen back to the menu camera and removes everything. Safe to call more than once.</summary>
        public void Dispose()
        {
            if (disposed) return;
            if (!Swapped) { Swapped = true; RestoreWorld(); }
            disposed = true;
            StopAudio();
            if (cam != null) { cam.gameObject.SetActive(false); Destroy(cam.gameObject); cam = null; } // never two listeners at once
            foreach (var b in disabled) if (b != null) b.enabled = true;
            disabled.Clear();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // Destroyed from outside (scene teardown): still give the cameras and settings back.
            if (!disposed)
            {
                disposed = true;
                if (!Swapped) RestoreWorld();
                foreach (var b in disabled) if (b != null) b.enabled = true;
            }
        }
    }
}
