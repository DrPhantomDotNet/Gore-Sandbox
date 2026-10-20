using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarEclipse
{
    /// <summary>
    /// The "????" boss realm (singleplayer only). Lives on the map's world root, so unloading the map (leave, restart,
    /// fake crash, finale) destroys it and <see cref="OnDestroy"/> restores everything it changed.
    /// Flow: "???" dialog -> LEAVE screen -> blackout -> boss music + boss nextbot -> survive 7:00 (angry at 5:00)
    /// -> "Finish it off" red gun -> shoot it once -> void of light -> main menu. Sanity 100% = fake crash.
    /// </summary>
    public sealed class BossRealm : MonoBehaviour
    {
        public const string MapId = "realm";
        public static BossRealm I { get; private set; }
        public static bool FlashlightOn = true;
        public static bool Active => I != null;

        public const float AngryAt = 300f, FinaleAt = 420f;

        // ---- state
        public float Sanity { get; private set; }
        public float FightTime { get; private set; }
        bool setup, fighting, angry, finale, won, crashing;
        BossBot bot;
        Light flashlight, fillLight;
        Camera cam;
        Firearm redGun;
        float lastShots;
        float jumpUntil, jumpStart, flickerUntil, surviveTextUntil, sanityFlashUntil, lightT = -1f;
        AudioSource extraClock;

        // ---- saved values
        bool savedHudHidden, savedNoclipAllowed;
        float savedSpeedMul;
        float savedParticleLight;

        // ================================================================== map

        public static MapDef CreateDef() => new MapDef
        {
            Id = MapId, Name = "????", Size = "??? · void", Glyph = "skull", DefaultTime = TimeOfDay.Night, Indoor = false, Ambience = "boss_clock",
            Description = "Something is waiting in the dark. It has been counting the seconds since you arrived. Don't come here.",
            Features = new[] { "???", "Singleplayer only", "Do not enter" },
            ThemeTop = new Color(0f, 0f, 0f), ThemeBottom = new Color(0.12f, 0f, 0.02f), Accent = new Color(1f, 0.2f, 0.2f),
            Build = Build, Thumbnail = BossArt.Thumbnail(),
        };

        static void Build(MapBuilder m)
        {
            float size = 56f;
            m.Block("Void Floor", new Vector3(-size, -1, -size), new Vector3(size, 0, size), Mats.Lit, SurfaceType.Concrete, new Color(0.11f, 0.11f, 0.12f), 0.25f);
            var rnd = new System.Random(4096);
            for (int i = 0; i < 26; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, d = 12f + (float)rnd.NextDouble() * 40f;
                var p = new Vector3(Mathf.Cos(ang) * d, 0, Mathf.Sin(ang) * d);
                float h = 4f + (float)rnd.NextDouble() * 9f, w = 0.6f + (float)rnd.NextDouble() * 1.2f;
                m.Box("Monolith", p + Vector3.up * h * 0.5f, Quaternion.Euler(0, (float)rnd.NextDouble() * 90f, (float)rnd.NextDouble() * 6f - 3f), new Vector3(w, h, w * 0.4f), Mats.Lit, SurfaceType.Concrete, new Color(0.05f, 0.05f, 0.055f));
            }
            m.PlayerSpawn = new Vector3(0, 0.1f, 0);
            m.PlayerYaw = 0f;
            m.Playable = new Bounds(new Vector3(0, 5f, 0), new Vector3(size * 2f, 14f, size * 2f));
            m.Boundaries(new Bounds(new Vector3(0, 10f, 0), new Vector3(size * 2f - 1f, 24f, size * 2f - 1f)));
            m.Options.Nextbots = false;
            m.Root.gameObject.AddComponent<BossRealm>();
        }

        // ================================================================== lifecycle

        void Awake()
        {
            I = this;
            FlashlightOn = true;
            savedHudHidden = GameManager.I != null && GameManager.I.HudHidden;
            savedNoclipAllowed = Rules.NoclipAllowed;
            savedSpeedMul = Rules.PlayerSpeedMultiplier;
            savedParticleLight = Shader.GetGlobalFloat("_SEG_ParticleLight");
            ApplyVoid(0f);
            UIRoot.HudLayer += DrawHud;
            UIRoot.TopLayer += DrawTop;
        }

        void OnDestroy()
        {
            UIRoot.HudLayer -= DrawHud;
            UIRoot.TopLayer -= DrawTop;
            if (I == this) I = null;
            if (GameManager.I != null) GameManager.I.HudHidden = savedHudHidden;
            Rules.NoclipAllowed = savedNoclipAllowed;
            Rules.PlayerSpeedMultiplier = savedSpeedMul;
            Shader.SetGlobalFloat("_SEG_ParticleLight", savedParticleLight > 0f ? savedParticleLight : 1f);
            // Lighting/fog/sky are re-applied by whatever loads next (menu backdrop or another map); make sure the
            // void can't leak anyway.
            RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.07f);
            RenderSettings.fogDensity = 0.01f;
            RenderSettings.ambientIntensity = 1f;
            FlashlightOn = true;
        }

        /// <summary>k = 0: pitch-black void; k = 1: void of light.</summary>
        void ApplyVoid(float k)
        {
            var dark = new Color(0.004f, 0.004f, 0.006f);
            var bright = new Color(1f, 1f, 1f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.025f, 0.025f, 0.03f), new Color(1.2f, 1.2f, 1.25f), k);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(dark, bright, k);
            RenderSettings.fogDensity = Mathf.Lerp(0.07f, 0.045f, k);
            RenderSettings.skybox = null;
            RenderSettings.reflectionIntensity = Mathf.Lerp(0f, 0.6f, k);
            if (MapLighting.Sun != null) { MapLighting.Sun.intensity = Mathf.Lerp(0f, 1.2f, k); MapLighting.Sun.color = Color.white; }
            Shader.SetGlobalFloat("_SEG_ParticleLight", 1f);
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor; }
        }

        void SetupPlayer(PlayerController p)
        {
            setup = true;
            cam = p.Cam;
            ApplyVoid(0f);
            // Flashlight: the light rides on the camera so it keeps working whatever is in your hands.
            var go = new GameObject("Flashlight Beam");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0.18f, -0.15f, 0.2f);
            go.transform.localRotation = Quaternion.Euler(1.5f, -1.5f, 0);
            flashlight = go.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.range = 32f;
            flashlight.spotAngle = 58f;
            flashlight.intensity = 3.2f;
            flashlight.color = new Color(1f, 0.95f, 0.82f);
            flashlight.shadows = LightShadows.Hard;
            flashlight.renderMode = LightRenderMode.ForcePixel;
            var fill = new GameObject("Flashlight Spill");
            fill.transform.SetParent(cam.transform, false);
            fill.transform.localPosition = new Vector3(0, 0, 1.2f);
            fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 5f;
            fillLight.intensity = 0.35f;
            fillLight.color = new Color(1f, 0.95f, 0.85f);
            // Inventory: only the flashlight.
            p.Inventory.Strip();
            p.Inventory.GiveCustom(Flashlight.Create(), 0, true);
            if (p.Noclip) p.SetNoclip(false);
            StartCoroutine(Run());
        }

        void Update()
        {
            var gm = GameManager.I;
            var p = PlayerController.Instance;
            if (gm == null) return;
            // Lockdown: no noclip, no spawn menu, no dev panel, normal speed.
            Rules.NoclipAllowed = false;
            Rules.PlayerSpeedMultiplier = 1f;
            if (Rules.TimeScale != 1f) Rules.TimeScale = 1f;
            gm.HudHidden = true;
            if (p != null && p.Noclip) p.SetNoclip(false);
            var sm = UIRoot.Find<SpawnMenuScreen>();
            if (sm != null) { UIRoot.Remove(sm); AudioManager.Ui("ui_error"); }
            if (DevPanel.Open) DevPanel.Toggle();

            if (!setup && p != null && p.Cam != null && gm.State == GameState.Playing) SetupPlayer(p);
            if (!setup) return;

            if (flashlight != null)
            {
                bool on = FlashlightOn || redGun != null;
                float flick = finale && !won && Time.unscaledTime < flickerUntil ? (Random.value < 0.5f ? 0f : 1f) : 1f;
                flashlight.enabled = on && flick > 0f;
                fillLight.enabled = flashlight.enabled;
            }
            if (cam != null && lightT < 0f) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor; }

            if (fighting && !won && !crashing)
            {
                FightTime += Time.deltaTime;
                if (!angry && FightTime >= AngryAt) GetAngry();
                if (!finale && FightTime >= FinaleAt) StartCoroutine(Finale());
                if (finale && redGun != null) CheckShot();
            }

            if (lightT >= 0f)
            {
                lightT += Time.unscaledDeltaTime / 3.5f;
                ApplyVoid(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lightT)));
            }
        }

        // ================================================================== story

        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            int choice = -1;
            UIRoot.Push(new RealmDialog("Hello.. You joined my realm.. Why?", new[] { "Because I wanted to", "I don't know", "Sorry, I'll leave.." }, c =>
            {
                choice = c;
                if (c == 2) KickOut(); // destroys this realm
            }));
            while (choice < 0) yield return null;
            if (choice == 2) yield break;
            if (choice == 1) yield return Say("You don't know? ...Then I will decide for you.", 1.6f);
            yield return Say("Oh, so you think you have a choice now?", 1.8f);
            bool done = false;
            UIRoot.Push(new LeaveScreen(OnBlackout, () => done = true));
            while (!done) yield return null;
            StartFight();
        }

        IEnumerator Say(string text, float hold)
        {
            var d = UIRoot.Push(new RealmDialog(text, null, null, hold));
            while (!d.Closed) yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
        }

        static void KickOut()
        {
            GameManager.I.ReturnToMenu();
            UIRoot.Push(new RealmKickedScreen());
        }

        void OnBlackout()
        {
            AudioManager.PlayAmbience("boss_clock_fast", 0.85f);
        }

        void StartFight()
        {
            fighting = true;
            FightTime = 0f;
            AudioManager.PlayMusic("boss_music", 0.85f);
            AudioManager.Play2D("boss_slam", 0.9f, 0.6f, AudioBus.Sfx);
            var p = PlayerController.Instance;
            var fwd = p != null ? MathX.Flat(p.Cam.transform.forward).normalized : Vector3.forward;
            var pos = (p != null ? p.Position : Vector3.zero) + Quaternion.Euler(0, Random.Range(-40f, 40f), 0) * fwd * 30f;
            pos = ClampToArena(pos);
            bot = BossBot.Spawn(transform, pos);
            surviveTextUntil = Time.unscaledTime + 4f;
            CameraFX.Shake(0.5f);
        }

        void GetAngry()
        {
            angry = true;
            if (bot != null)
            {
                bot.MakeAngry();
                AudioManager.Play("boss_scream", bot.transform.position + Vector3.up * 1.5f, 1f, 0.8f, 0.6f, 4f, 200f);
            }
            AudioManager.Play2D("boss_scream", 0.7f, 0.75f, AudioBus.Sfx);
            CameraFX.Shake(0.7f);
            CameraFX.Flash(new Color(0.5f, 0f, 0f), 0.6f);
            // Ticking intensifies: a second, faster clock on top.
            extraClock = AudioManager.CreateLoop("boss_clock_fast", transform, 0.8f, AudioBus.Ambience, 500f);
            if (extraClock != null) { extraClock.spatialBlend = 0f; extraClock.pitch = 1.5f; }
            Stats.UnlockById("now_angry");
        }

        IEnumerator Finale()
        {
            finale = true;
            flickerUntil = Time.unscaledTime + 2.2f;
            AudioManager.Play2D("boss_glitch", 0.7f, 0.8f, AudioBus.Sfx);
            yield return new WaitForSecondsRealtime(1.2f);
            Jumpscare(1.1f);
            yield return new WaitForSecondsRealtime(1.0f);
            GiveRedGun();
        }

        void GiveRedGun()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            var g = WeaponCatalog.Get("glock");
            var def = new WeaponDef
            {
                Id = "glock", Name = "Finish it off", Kind = WeaponKind.Firearm, Glyph = "gun", Description = "Finish it off.",
                Damage = 999, Rpm = 240, HipSpread = 0.2f, AimSpread = 0f, Recoil = 2.6f, Penetration = 2f, Force = 30, Caliber = 9f, Mag = 99,
                ReloadTime = g.ReloadTime, EmptyExtra = g.EmptyExtra, FireSound = "gun_admin", MagOut = g.MagOut, MagIn = g.MagIn, Charge = g.Charge,
                HipPos = g.HipPos, AimFov = g.AimFov, AimDistance = g.AimDistance, CasingType = 0, WeightKg = g.WeightKg, NoiseRadius = 10f,
            };
            redGun = new Firearm { Def = def };
            p.Inventory.GiveCustom(redGun, 1, true);
            var red = Mats.Glow(new Color(0.85f, 0.03f, 0.03f), 1.6f);
            if (redGun.Model != null)
                foreach (var r in redGun.Model.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial == Mats.Lit || r.sharedMaterial == Mats.LitMetal) r.sharedMaterial = red;
            lastShots = Stats.Get(Stats.ShotsFired);
            AudioManager.Play2D("boss_slam", 1f, 0.75f, AudioBus.Sfx);
        }

        void CheckShot()
        {
            float shots = Stats.Get(Stats.ShotsFired);
            if (shots <= lastShots) return;
            lastShots = shots;
            var p = PlayerController.Instance;
            if (p == null || bot == null || p.Inventory.Current != redGun) return;
            var o = p.Cam.transform.position;
            var dir = p.Cam.transform.forward;
            var c = bot.transform.position + Vector3.up * 1.5f;
            float along = Vector3.Dot(c - o, dir);
            if (along <= 0f) return;
            float miss = Vector3.Distance(o + dir * along, c);
            if (miss > 1.35f) return;
            if (Physics.Raycast(o, dir, out var hit, along - 0.5f, Layers.SightMask, QueryTriggerInteraction.Ignore)) return;
            Win();
        }

        void Win()
        {
            won = true;
            fighting = false;
            AudioManager.StopMusic();
            AudioManager.PlayAmbience("boss_light", 0.8f);
            if (extraClock != null) extraClock.Stop();
            AudioManager.Play2D("boss_scream", 1f, 0.6f, AudioBus.Sfx);
            AudioManager.Play2D("boss_shimmer", 1f, 1f, AudioBus.Sfx);
            CameraFX.Flash(Color.white, 1f);
            CameraFX.Shake(0.8f);
            if (bot != null) bot.Die();
            lightT = 0f;
            Stats.UnlockById("realm_owner");
            GameManager.I.StartCoroutine(ReturnLater(5.5f));
        }

        static IEnumerator ReturnLater(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (GameManager.I != null && GameManager.I.CurrentMap != null && GameManager.I.CurrentMap.Id == MapId && I != null) GameManager.I.ReturnToMenu();
        }

        // ================================================================== nextbot contact / sanity

        public void OnBotTouch(BossBot b)
        {
            if (won || crashing) return;
            Jumpscare(0.9f);
            Sanity = Mathf.Min(100f, Sanity + 5f);
            sanityFlashUntil = Time.unscaledTime + 1.2f;
            var p = PlayerController.Instance;
            if (p != null) b.Retreat(p.Position);
            if (Sanity >= 100f) StartCoroutine(Crash());
        }

        void Jumpscare(float duration)
        {
            jumpStart = Time.unscaledTime;
            jumpUntil = Time.unscaledTime + duration;
            AudioManager.Play2D("boss_jumpscare", 1f, Random.Range(0.95f, 1.05f), AudioBus.Sfx);
            CameraFX.Shake(1f);
            CameraFX.Flash(new Color(0.3f, 0f, 0f), 0.7f);
        }

        IEnumerator Crash()
        {
            crashing = true;
            yield return new WaitForSecondsRealtime(0.95f); // let the jumpscare land first
            jumpUntil = 0f;
            yield return new WaitForEndOfFrame();
            Texture2D shot = null;
            try { shot = ScreenCapture.CaptureScreenshotAsTexture(); } catch { shot = null; }
            UIRoot.Push(new FakeCrashScreen(shot));
        }

        static Vector3 ClampToArena(Vector3 p) => new Vector3(Mathf.Clamp(p.x, -50f, 50f), 0f, Mathf.Clamp(p.z, -50f, 50f));
        public static Vector3 Clamp(Vector3 p) => ClampToArena(p);

        // ================================================================== HUD

        void DrawHud()
        {
            if (GameManager.I == null || GameManager.I.State != GameState.Playing || !setup) return;
            if (UIRoot.Find<LeaveScreen>() != null || UIRoot.Find<FakeCrashScreen>() != null) return;
            float W = UI.VW, H = UI.VH, t = Time.unscaledTime;

            // Sanity (top-right).
            int stage = Sanity >= 50f ? 2 : Sanity >= 10f ? 1 : 0;
            var panel = new Rect(W - 370, 24, 346, 108);
            bool flash = t < sanityFlashUntil;
            float jx = stage == 2 ? Random.Range(-2f, 2f) : 0f;
            panel.x += jx;
            UI.Rounded(panel, new Color(0.02f, 0.02f, 0.03f, 0.78f));
            UI.Outline(panel, flash ? new Color(1f, 0.1f, 0.1f, 0.9f) : new Color(1f, 1f, 1f, 0.12f), 2);
            var icon = new Rect(panel.x + 14, panel.y + 10, 88, 88);
            if (flash) icon = new Rect(icon.x - 6, icon.y - 6, icon.width + 12, icon.height + 12);
            UI.Tex(icon, BossArt.SanityIcon(stage), Color.white);
            var col = Color.Lerp(new Color(0.85f, 0.95f, 0.85f), new Color(1f, 0.15f, 0.15f), Sanity / 100f);
            UI.Label(new Rect(panel.x + 116, panel.y + 10, 220, 34), "Sanity", 24, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (stage == 2) BossArt.GlitchText(new Rect(panel.x + 116, panel.y + 40, 220, 50), Mathf.RoundToInt(Sanity) + "%", 44, col, 0.6f, UI.HeavyFont, TextAnchor.MiddleLeft);
            else UI.Label(new Rect(panel.x + 116, panel.y + 40, 220, 50), Mathf.RoundToInt(Sanity) + "%", 44, col, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Fill(new Rect(panel.x + 116, panel.yMax - 14, 214, 5), new Color(1, 1, 1, 0.1f));
            UI.Fill(new Rect(panel.x + 116, panel.yMax - 14, 214 * Sanity / 100f, 5), col);

            // Clock (top-centre).
            if (fighting || won)
            {
                int secs = Mathf.FloorToInt(FightTime);
                string clock = (secs / 60) + ":" + (secs % 60).ToString("00");
                var cr = new Rect(W / 2f - 150, 20, 300, 92);
                UI.Rounded(cr, new Color(0.02f, 0.02f, 0.03f, 0.7f));
                float pulse = (FightTime % 1f) < 0.12f ? 1f : 0f;
                var cc = angry ? new Color(1f, 0.25f + 0.2f * pulse, 0.2f) : new Color(0.92f, 0.92f, 0.95f);
                UI.Label(new Rect(cr.x, cr.y + 2, cr.width, 62), clock, 54, cc, TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
                string sub = won ? "IT'S OVER" : finale ? "FINISH IT" : "SURVIVE UNTIL 7:00";
                UI.Label(new Rect(cr.x, cr.y + 60, cr.width, 26), sub, 16, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter, FontStyle.Bold);
                if (t < surviveTextUntil)
                {
                    float a = Mathf.Clamp01((surviveTextUntil - t) / 1f);
                    BossArt.GlitchText(new Rect(0, H * 0.3f, W, 160), "SURVIVE", 130, new Color(1f, 0.9f, 0.9f, a), 1f);
                }
            }

            if (finale && !won && redGun != null)
            {
                BossArt.GlitchText(new Rect(0, H * 0.16f, W, 140), "FINISH IT OFF", 96, new Color(1f, 0.08f, 0.08f, 0.9f), 1.6f);
                // Crosshair (the normal HUD is hidden in the realm).
                UI.Fill(new Rect(W / 2f - 2, H / 2f - 2, 4, 4), new Color(1f, 0.2f, 0.2f, 0.9f));
                UI.Outline(new Rect(W / 2f - 12, H / 2f - 12, 24, 24), new Color(1f, 0.2f, 0.2f, 0.5f), 1);
            }
            if (won && lightT >= 0f)
            {
                float a = Mathf.Clamp01(lightT * 1.5f);
                UI.Label(new Rect(0, H * 0.4f, W, 120), "This realm is yours now.", 64, new Color(0.15f, 0.15f, 0.2f, a), TextAnchor.MiddleCenter, FontStyle.Normal, false, false, UI.HeavyFont);
            }
        }

        void DrawTop()
        {
            if (!setup) return;
            float W = UI.VW, H = UI.VH, t = Time.unscaledTime;
            // Finale flicker.
            if (finale && !won && t < flickerUntil && Mathf.FloorToInt(t * 18f) % 3 == 0)
                UI.Fill(new Rect(0, 0, W, H), Random.value < 0.5f ? Color.black : new Color(1f, 1f, 1f, 0.6f));
            // Jumpscare: the face fills the screen.
            if (t < jumpUntil)
            {
                float k = t - jumpStart;
                UI.Fill(new Rect(0, 0, W, H), Mathf.FloorToInt(k * 24f) % 3 == 0 ? new Color(0.35f, 0f, 0f) : Color.black);
                float s = H * Mathf.Lerp(1.9f, 1.25f, Mathf.Clamp01(k * 6f));
                float jx = Random.Range(-40f, 40f), jy = Random.Range(-40f, 40f);
                UI.Tex(new Rect(W / 2f - s / 2f + jx, H / 2f - s / 2f + jy, s, s), BossArt.BossFace(), Color.white);
                if (Random.value < 0.3f) UI.Tex(new Rect(W / 2f - s / 2f + jx + 18, H / 2f - s / 2f + jy, s, s), BossArt.BossFace(), new Color(1f, 0f, 0f, 0.4f));
            }
        }
    }

    // ====================================================================== the boss nextbot

    /// <summary>
    /// The realm's nextbot. Slow, glowing, always searching. Touching it doesn't kill: it jumpscares you, adds sanity
    /// and vanishes to somewhere else for a few seconds. Not in <see cref="Nextbots.Defs"/>, so never spawnable.
    /// </summary>
    public sealed class BossBot : MonoBehaviour
    {
        public float Speed = 3.6f;
        public const float AngrySpeed = 4.8f; // player sprint is 7.2
        Transform quad, halo;
        Light glow;
        AudioSource loop;
        float bob, cooldown, senseT, hideUntil, dieT = -1f;
        Vector3 lastKnown;
        bool angry;
        static Material faceMat, haloMat;

        public static BossBot Spawn(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Boss Nextbot");
            go.layer = Layers.Nextbot;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var q = new GameObject("Face");
            q.layer = Layers.Nextbot;
            q.transform.SetParent(go.transform, false);
            var mb = new MeshBuilder();
            mb.Face(Vector3.zero, Vector3.back, Vector3.up, 1.35f, 1.35f);
            mb.Face(Vector3.zero, Vector3.forward, Vector3.up, 1.35f, 1.35f);
            q.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("BossQuad");
            if (faceMat == null) faceMat = Mats.ParticleWith(BossArt.BossFace());
            q.AddComponent<MeshRenderer>().sharedMaterial = faceMat;
            var h = new GameObject("Halo");
            h.layer = Layers.Nextbot;
            h.transform.SetParent(go.transform, false);
            var hb = new MeshBuilder { Color = new Color(1f, 0.15f, 0.7f, 0.8f) };
            hb.Face(Vector3.zero, Vector3.back, Vector3.up, 2.6f, 2.6f);
            hb.Face(Vector3.zero, Vector3.forward, Vector3.up, 2.6f, 2.6f);
            h.AddComponent<MeshFilter>().sharedMesh = hb.ToMesh("BossHalo");
            if (haloMat == null) haloMat = Mats.AdditiveWith(BossArt.Glow());
            h.AddComponent<MeshRenderer>().sharedMaterial = haloMat;
            var bb = go.AddComponent<BossBot>();
            bb.quad = q.transform;
            bb.halo = h.transform;
            var lg = new GameObject("Glow");
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(0, 1.5f, 0);
            bb.glow = lg.AddComponent<Light>();
            bb.glow.type = LightType.Point;
            bb.glow.color = new Color(1f, 0.2f, 0.75f);
            bb.glow.range = 9f;
            bb.glow.intensity = 2.2f;
            bb.loop = AudioManager.CreateLoop("nextbot_chase", go.transform, 0.9f, AudioBus.Sfx, 40f);
            if (bb.loop != null) bb.loop.pitch = 0.55f;
            bb.lastKnown = pos;
            AudioManager.Play("boss_scream", pos + Vector3.up * 1.5f, 1f, 0.7f, 1f, 4f, 200f);
            return bb;
        }

        public void MakeAngry()
        {
            angry = true;
            Speed = AngrySpeed;
            if (glow != null) { glow.color = new Color(1f, 0.08f, 0.08f); glow.range = 12f; glow.intensity = 3f; }
            if (loop != null) loop.pitch = 0.75f;
        }

        public void Retreat(Vector3 playerPos)
        {
            float ang = Random.Range(0f, 360f);
            var p = BossRealm.Clamp(playerPos + Quaternion.Euler(0, ang, 0) * Vector3.forward * Random.Range(24f, 32f));
            transform.position = p;
            cooldown = angry ? 2.5f : 3.5f;
            hideUntil = Time.time + 1.2f;
            lastKnown = playerPos;
        }

        public void Die()
        {
            dieT = 0f;
            if (loop != null) loop.Stop();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bob += dt * (angry ? 9f : 5f);
            var cam = Camera.main != null ? Camera.main.transform : null;
            var p = PlayerController.Instance;
            if (p != null && p.Cam != null) cam = p.Cam.transform;
            if (cam != null)
            {
                var flat = MathX.Flat(cam.position - transform.position);
                if (flat.sqrMagnitude > 0.01f) { quad.rotation = Quaternion.LookRotation(-flat.normalized, Vector3.up); halo.rotation = quad.rotation; }
            }
            float jitter = angry ? 0.05f : 0.02f;
            quad.localPosition = new Vector3(Random.Range(-jitter, jitter), 1.5f + Mathf.Sin(bob) * 0.06f, 0);
            halo.localPosition = quad.localPosition + quad.forward * 0.05f;
            halo.localScale = Vector3.one * (1f + Mathf.Sin(bob * 1.7f) * 0.08f);
            if (glow != null) glow.intensity = (angry ? 3f : 2.2f) * (0.8f + 0.2f * Mathf.Sin(bob * 2.3f));

            if (dieT >= 0f)
            {
                dieT += Time.unscaledDeltaTime;
                float s = Mathf.Max(0f, 1f - dieT / 1.2f);
                quad.localScale = new Vector3(1f + dieT * 2f, s, 1f);
                halo.localScale = Vector3.one * (1f + dieT * 6f);
                if (glow != null) { glow.color = Color.white; glow.intensity = 6f * s; glow.range = 30f; }
                if (dieT > 1.3f) Destroy(gameObject);
                return;
            }

            bool hidden = Time.time < hideUntil;
            quad.gameObject.SetActive(!hidden);
            halo.gameObject.SetActive(!hidden);
            if (glow != null) glow.enabled = !hidden;
            if (hidden || p == null || BossRealm.I == null) return;

            // Search: it always roughly knows where you are, and locks on when close.
            var target = p.Position;
            float dist = MathX.Flat(target - transform.position).magnitude;
            senseT -= dt;
            if (dist < (angry ? 40f : 26f)) lastKnown = target;
            else if (senseT <= 0f)
            {
                senseT = angry ? 2f : 4f;
                var r = Random.insideUnitCircle * (angry ? 4f : 9f);
                lastKnown = target + new Vector3(r.x, 0, r.y);
            }
            var to = MathX.Flat(lastKnown - transform.position);
            if (to.magnitude > 0.3f)
            {
                var next = transform.position + to.normalized * Mathf.Min(to.magnitude, Speed * dt);
                next.y = 0f;
                transform.position = BossRealm.Clamp(next);
            }

            cooldown -= dt;
            if (cooldown <= 0f && dist < 1.15f && Mathf.Abs(target.y - transform.position.y) < 2.5f)
            {
                cooldown = 3f;
                BossRealm.I.OnBotTouch(this);
            }
        }
    }
}
