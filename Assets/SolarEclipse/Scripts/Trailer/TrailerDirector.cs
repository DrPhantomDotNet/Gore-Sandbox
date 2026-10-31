using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SolarEclipse
{
    /// <summary>
    /// SolarEclipse Productions trailer (private build): EXTRAS > PLAY ANIMATION. A 2:00 cinematic made live in the
    /// engine on the TrailerSets studio map, locked to TrailerMusic (128 BPM). Every shot is scripted here: camera
    /// moves, lighting, slow motion, puppet animation (TrailerActor) that hands over to real ragdoll gore on each hit,
    /// cars, tracers, explosions, colour grading, impact frames and title cards. Esc stops it.
    /// </summary>
    public sealed class TrailerDirector : MonoBehaviour
    {
        public static TrailerDirector I { get; private set; }
        public static bool Pending;
        static AudioClip musicClip;
        static float[] musicData;
        static float musicProgress;
        static bool generating;

        // ------------------------------------------------------------------ start / stop

        /// <summary>EXTRAS > PLAY ANIMATION.</summary>
        public static void Begin()
        {
            if (Pending || I != null) return;
            UIRoot.Push(new TrailerPrepScreen());
            if (musicClip == null && !generating)
            {
                generating = true;
                System.Threading.Tasks.Task.Run(() =>
                {
                    try { musicData = TrailerMusic.Generate(p => musicProgress = p); }
                    catch (Exception e) { Debug.LogException(e); musicData = new float[TrailerMusic.Rate]; }
                    generating = false;
                });
            }
        }

        public static float PrepProgress => musicClip != null ? 1f : musicProgress;

        /// <summary>Called by the prep screen every frame; starts the studio map when the music is ready.</summary>
        public static bool TickPrep()
        {
            if (musicClip == null)
            {
                if (generating || musicData == null) return false;
                musicClip = AudioClip.Create("TrailerMusic", musicData.Length, 1, TrailerMusic.Rate, false);
                musicClip.SetData(musicData, 0);
                musicData = null;
            }
            Pending = true;
            saved = new SavedSettings
            {
                gore = Settings.Data.goreLevel, hints = Settings.Data.tutorialHints, ragdolls = Settings.Data.maxRagdolls,
                shake = Settings.Data.screenShake, fog = Settings.Data.fog,
            };
            Settings.Data.goreLevel = 6; Settings.Data.tutorialHints = false; Settings.Data.maxRagdolls = 60; Settings.Data.fog = true;
            Toasts.Hidden = true;
            GameManager.I.StartMap(TrailerSets.CreateDef(), new MapOptions { Time = TimeOfDay.Night, StartingNpcs = 0, Loadout = 0 });
            return true;
        }

        struct SavedSettings { public int gore, ragdolls; public bool hints, fog; public float shake; }
        static SavedSettings saved;

        static void RestoreSettings()
        {
            Settings.Data.goreLevel = saved.gore; Settings.Data.tutorialHints = saved.hints; Settings.Data.maxRagdolls = saved.ragdolls;
            Settings.Data.screenShake = saved.shake; Settings.Data.fog = saved.fog;
            Toasts.Hidden = false;
        }

        public void Stop()
        {
            if (stopped) return;
            stopped = true;
            Pending = false;
            if (music != null) music.Stop();
            Rules.TimeScale = 1f;
            RestoreSettings();
            if (GameManager.I != null) { GameManager.I.HudHidden = false; GameManager.I.ReturnToMenu(); }
        }

        bool started, stopped;
        AudioSource music;
        Camera cam;
        TrailerScreen screen;

        void Awake() { I = this; }
        void OnDestroy()
        {
            if (I == this) I = null;
            if (cam != null) Destroy(cam.gameObject);
            Rules.TimeScale = 1f;
            if (!stopped) { Pending = false; RestoreSettings(); }
        }

        void StartTrailer()
        {
            started = true;
            Pending = false;
            var gm = GameManager.I;
            gm.HudHidden = true;
            var p = PlayerController.Instance;
            if (p != null) p.gameObject.SetActive(false);
            var cg = new GameObject("Trailer Camera");
            cam = cg.AddComponent<Camera>();
            cam.depth = 50; cam.nearClipPlane = 0.03f; cam.farClipPlane = 900f; cam.fieldOfView = 45f; cam.clearFlags = CameraClearFlags.Skybox;
            cg.AddComponent<AudioListener>();
            music = gameObject.AddComponent<AudioSource>();
            music.clip = musicClip; music.spatialBlend = 0f; music.volume = Mathf.Clamp01(Settings.Data.masterVolume + 0.1f); music.ignoreListenerPause = true;
            screen = UIRoot.Push(new TrailerScreen(this));
            AudioManager.PlayAmbience(null);
            music.Play();
            StartCoroutine(Script());
        }

        // ------------------------------------------------------------------ timeline

        /// <summary>Music time (seconds).</summary>
        public float T => music != null && music.isPlaying ? music.time : endTime;
        float endTime;
        static float B(float bar) => TrailerMusic.BarTime(bar);
        IEnumerator At(float t) { while (T < t && !stopped) yield return null; }

        void Update()
        {
            if (stopped) return;
            if (!started)
            {
                if (GameManager.I != null && GameManager.I.State == GameState.Playing && UIRoot.Count == 0) StartTrailer();
                return;
            }
            if (music != null && !music.isPlaying && endTime == 0f) endTime = music.clip != null ? music.clip.length : 120f;
            float dt = Time.unscaledDeltaTime;
            UpdateSlowMo();
            UpdateCamera(dt);
            UpdateLights();
            UpdateFx(dt);
        }

        // ------------------------------------------------------------------ slow motion windows

        readonly List<(float from, float to, float scale)> slow = new List<(float, float, float)>();
        void SlowMo(float from, float to, float scale) => slow.Add((from, to, scale));

        void UpdateSlowMo()
        {
            float s = 1f, t = T;
            foreach (var w in slow)
                if (t >= w.from && t < w.to)
                {
                    // Ease in/out of the ramp over 0.12 s of music time.
                    float k = Mathf.Min(Mathf.Clamp01((t - w.from) / 0.12f), Mathf.Clamp01((w.to - t) / 0.12f));
                    s = Mathf.Min(s, Mathf.Lerp(1f, w.scale, k));
                }
            Rules.TimeScale = s;
        }

        // ------------------------------------------------------------------ camera

        Func<float, (Vector3 pos, Vector3 look, float fov)> camFn;
        float camFrom, camTo, trauma, punch, roll;
        Vector3 smoothLook;

        void Shot(float from, float to, Func<float, (Vector3, Vector3, float)> f) { camFn = f; camFrom = from; camTo = to; }
        static float E(float u) => u * u * (3f - 2f * u);
        static Vector3 Orbit(Vector3 c, float r, float h, float deg) => c + Quaternion.Euler(0, deg, 0) * Vector3.back * r + Vector3.up * h;

        void UpdateCamera(float dt)
        {
            if (cam == null || camFn == null) return;
            float u = Mathf.Clamp01((T - camFrom) / Mathf.Max(0.01f, camTo - camFrom));
            var (pos, look, fov) = camFn(u);
            trauma = Mathf.MoveTowards(trauma, 0f, dt * 1.6f);
            punch = Mathf.MoveTowards(punch, 0f, dt * 40f);
            float sh = trauma * trauma;
            float tt = Time.unscaledTime * 28f;
            var shake = new Vector3(Mathf.PerlinNoise(tt, 1f) - 0.5f, Mathf.PerlinNoise(2f, tt) - 0.5f, 0f) * sh * 0.5f;
            cam.transform.position = pos;
            var fwd = look - pos;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            cam.transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(shake.y * 14f, shake.x * 14f, roll + (Mathf.PerlinNoise(tt, 5f) - 0.5f) * sh * 8f);
            cam.fieldOfView = Mathf.Clamp(fov - punch, 8f, 110f);
        }

        void Shake(float a) => trauma = Mathf.Clamp01(Mathf.Max(trauma, a));
        void Punch(float deg) => punch = Mathf.Max(punch, deg);

        // ------------------------------------------------------------------ lighting

        void Lighting(TimeOfDay time, Color fog, float density)
        {
            if (WorldRoot.Current != null) MapLighting.Apply(time, false, WorldRoot.Current.transform);
            RenderSettings.fog = true;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = density;
        }

        bool alarm;
        float alarmFrom;
        void UpdateLights()
        {
            float t = Time.time;
            for (int i = 0; i < TrailerSets.FireLights.Count; i++)
            {
                var l = TrailerSets.FireLights[i];
                if (l != null) l.intensity = 2.2f + Mathf.PerlinNoise(t * 6f, i * 3.1f) * 1.6f;
            }
            float a = alarm ? (Mathf.Repeat(T - alarmFrom, 0.5f) < 0.25f ? 1f : 0.15f) : 0f;
            foreach (var l in TrailerSets.AlarmLights) if (l != null) l.intensity = a * 5f;
            foreach (var l in TrailerSets.NeonLights) if (l != null) l.intensity = 1.4f + 0.6f * Pulse;
            if (TrailerSets.VoidLight != null) TrailerSets.VoidLight.intensity = 1.2f + heart * 6f;
        }

        // ------------------------------------------------------------------ overlay effects

        public float Black = 1f, Letterbox = 1f;
        Color flashCol; float flashA, flashDecay = 4f;
        Color washCol; float washA, washPulse;
        int impactFrames; Color impactCol = Color.white;
        float chroma, lines, heart, ring = -1f;
        Color ringCol = Color.white;
        bool policePulse;

        void Flash(Color c, float a, float decay = 4f) { flashCol = c; flashA = Mathf.Max(flashA, a); flashDecay = decay; }
        void Wash(Color c, float a, float pulse = 0f) { washCol = c; washA = a; washPulse = pulse; }
        void Impact(Color c, int frames = 3) { impactCol = c; impactFrames = frames; chroma = 1f; lines = 1f; Shake(0.7f); Punch(8f); }
        void Ring(Color c) { ring = 0f; ringCol = c; }

        /// <summary>Kick drum envelope (0..1) during beat sections.</summary>
        public float Pulse
        {
            get
            {
                float t = T;
                bool beat = (t >= B(10) && t < B(31)) || (t >= B(32) && t < B(59));
                return beat ? Mathf.Exp(-Mathf.Repeat(t, TrailerMusic.Beat) * 9f) : 0f;
            }
        }

        static readonly float[] Heartbeats = { 0.8f, 2.6f, 4.4f, 6.2f, 8.0f, 58.6f, 117.2f };

        void UpdateFx(float dt)
        {
            flashA = Mathf.MoveTowards(flashA, 0f, dt * flashDecay);
            chroma = Mathf.MoveTowards(chroma, 0f, dt * 2.5f);
            lines = Mathf.MoveTowards(lines, 0f, dt * 2.2f);
            if (ring >= 0f) { ring += dt * 1.6f; if (ring > 1f) ring = -1f; }
            heart = 0f;
            foreach (var h in Heartbeats) { float d = T - h; if (d >= 0f && d < 0.6f) heart = Mathf.Max(heart, Mathf.Exp(-d * 6f)); }
            for (int i = embers.Count - 1; i >= 0; i--)
            {
                var e = embers[i];
                e.life -= dt; e.pos += e.vel * dt; e.vel += new Vector2(Mathf.Sin(e.life * 3f + i) * 30f, -20f) * dt;
                if (e.life <= 0f) embers.RemoveAt(i); else embers[i] = e;
            }
        }

        struct Ember { public Vector2 pos, vel; public float life, max, size; public Color col; }
        readonly List<Ember> embers = new List<Ember>();
        void Embers(Rect area, int n, Color col, float speed = 160f)
        {
            for (int i = 0; i < n; i++)
            {
                float l = Random.Range(0.8f, 2.4f);
                embers.Add(new Ember
                {
                    pos = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax)),
                    vel = new Vector2(Random.Range(-1f, 1f) * speed * 0.5f, -Random.Range(0.3f, 1f) * speed),
                    life = l, max = l, size = Random.Range(3f, 9f), col = col,
                });
            }
            if (embers.Count > 600) embers.RemoveRange(0, embers.Count - 600);
        }

        // ------------------------------------------------------------------ text cards

        enum Style { Thin, Slide, Slam, Small, Logo, Card }
        sealed class Card { public string Text; public float From, To; public Style Style; public float Y; public Color Color; public int Size; public bool Embered; }
        readonly List<Card> cards = new List<Card>();
        void Text(string text, float from, float to, Style st, float y = 0.5f, int size = 0, Color? col = null)
            => cards.Add(new Card { Text = text, From = from, To = to, Style = st, Y = y, Size = size, Color = col ?? Color.white });

        // Logo / end card / special overlays (time windows)
        float logoFrom = -1f, logoTo = -1f, logoHit = -1f, endFrom = -1f, starsFrom = -1f, starsTo = -1f;
        float featureFrom = -1f; int feature = -1;

        // ------------------------------------------------------------------ helpers for the script

        readonly List<GameObject> junk = new List<GameObject>();
        T2 Keep<T2>(T2 c) where T2 : Component { if (c != null) junk.Add(c.gameObject); return c; }

        void ClearScene()
        {
            foreach (var d in Goredoll.All.ToArray()) if (d != null) d.Despawn();
            Goredoll.All.Clear();
            foreach (var g in junk) if (g != null) Destroy(g);
            junk.Clear();
            Decals.ClearAll(); BloodFX.Clear(); FX.Clear(); Debris.ClearAll();
            slow.RemoveAll(w => w.to < T);
            Wash(Color.clear, 0f);
            alarm = false; policePulse = false; roll = 0f;
        }

        static CustomLook Look(string name, Color shirt, Color pants, bool sleeves = true, float height = 1.03f, float build = 1.1f, int hair = 1, Color? skin = null)
            => new CustomLook { Name = name, Shirt = shirt, Pants = pants, Shoes = new Color(0.06f, 0.06f, 0.07f), LongSleeves = sleeves, Height = height, Build = build, HairStyle = hair, Skin = skin ?? new Color(0.86f, 0.68f, 0.55f) };

        TrailerActor Actor(Vector3 feet, float yaw, CustomLook look = null, string preset = "Normal", string mask = null, int seed = 0)
        {
            var a = TrailerActor.Spawn(feet, yaw, look, preset, seed);
            if (a == null) return null;
            if (mask != null) OWOutfit.Attach(a.Part(PartType.Head), a.Part(PartType.Chest), a.Part(PartType.ForearmL), ";;;;;" + mask + ";", a.Doll.gameObject.layer);
            if (a.Weapon == null) a.Weapon = a.GetComponentInChildren<WeaponModel>();
            return a;
        }

        TrailerActor Hero(Vector3 feet, float yaw) => Actor(feet, yaw, Look("SolarEclipse", new Color(0.07f, 0.07f, 0.08f), new Color(0.09f, 0.09f, 0.1f), true, 1.06f, 1.18f, 1), "Normal", "m_skull", 777).Look(Expression.Angry);

        void Shoot(TrailerActor a, Vector3 target, string sound = "gun_ar15", float vol = 0.9f, bool tracer = true)
        {
            if (a == null) return;
            var m = a.Muzzle;
            var dir = (target - m).normalized;
            FX.Flash(m + dir * 0.1f, new Color(1f, 0.75f, 0.4f), 8f, 5f, 0.06f);
            FX.MuzzleSmoke(m, dir, 0.8f);
            FX.Sparks(m + dir * 0.15f, dir, 6, 9f);
            AudioManager.Play(sound, m, vol, Random.Range(0.95f, 1.05f), 1f, 3f, 80f);
            if (tracer) Tracer(m, target);
            Casing(m - dir * 0.3f, Quaternion.LookRotation(dir) * new Vector3(1.5f, 1.2f, 0f));
        }

        void Tracer(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Tracer");
            var mb = new MeshBuilder { Color = Color.white };
            mb.Box(Vector3.zero, new Vector3(0.025f, 0.025f, 1.6f));
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Tracer");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Glow(new Color(1f, 0.85f, 0.45f), 4f);
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
            var mv = go.AddComponent<TrailerMover>();
            mv.Velocity = (to - from).normalized * 180f; mv.Life = (to - from).magnitude / 180f;
            junk.Add(go);
        }

        void Casing(Vector3 pos, Vector3 vel)
        {
            var go = new GameObject("Casing");
            var mb = new MeshBuilder { Color = new Color(0.85f, 0.65f, 0.25f), Gloss = 0.9f };
            mb.Cylinder(new Vector3(0, 0, -0.012f), new Vector3(0, 0, 0.012f), 0.0045f, 0.0045f, 8);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Casing");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            go.transform.position = pos;
            go.layer = Layers.Debris;
            var c = go.AddComponent<CapsuleCollider>(); c.radius = 0.005f; c.height = 0.025f; c.direction = 2;
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.01f; rb.velocity = vel; rb.angularVelocity = Random.insideUnitSphere * 30f;
            Debris.Register(go, 8f);
        }

        void Money(Vector3 center, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Bill");
                var mb = new MeshBuilder { Color = new Color(0.45f, 0.75f, 0.45f) };
                mb.Box(Vector3.zero, new Vector3(0.16f, 0.004f, 0.07f));
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Bill");
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                go.layer = Layers.Debris;
                go.transform.SetPositionAndRotation(center + new Vector3(Random.Range(-3f, 3f), Random.Range(2.5f, 5f), Random.Range(-3f, 3f)), Random.rotation);
                go.AddComponent<BoxCollider>().size = new Vector3(0.16f, 0.004f, 0.07f);
                var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.01f; rb.drag = 6f; rb.angularDrag = 0.5f; rb.angularVelocity = Random.insideUnitSphere * 6f;
                junk.Add(go);
            }
        }

        OWCar Car(string model, string paint, string neon, Vector3 pos, float yaw, bool driven = true)
        {
            var go = OWCar.Create(OWCar.DefFor(model, paint, neon, model == "c_super", false, false), pos, Quaternion.Euler(0, yaw, 0));
            var car = go.GetComponent<OWCar>();
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            if (driven) car.DriverId = 0;
            junk.Add(go);
            return car;
        }

        /// <summary>Drives a (kinematic) car along a Catmull-Rom path between music times, sliding sideways by drift degrees.</summary>
        IEnumerator Drive(OWCar car, Vector3[] pts, float from, float to, float drift = 0f, Action<Vector3, Vector3> onMove = null)
        {
            float slide = 0f;
            while (T < to && !stopped && car != null)
            {
                float u = Mathf.Clamp01((T - from) / (to - from));
                var p = CatmullPath(pts, u);
                var ahead = CatmullPath(pts, Mathf.Min(1f, u + 0.01f));
                var further = CatmullPath(pts, Mathf.Min(1f, u + 0.05f));
                var dir = ahead - p; dir.y = 0;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    var dir2 = further - ahead; dir2.y = 0;
                    float bend = dir2.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(dir, dir2, Vector3.up) : 0f;
                    slide = Mathf.Lerp(slide, Mathf.Clamp(bend / 12f, -1f, 1f) * drift, 1f - Mathf.Exp(-6f * Time.deltaTime));
                    car.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, slide, 0);
                }
                var vel = (p - car.transform.position) / Mathf.Max(Time.deltaTime, 0.001f);
                car.transform.position = p;
                car.Steer = Mathf.Clamp(Vector3.SignedAngle(car.transform.forward, dir, Vector3.up) / 20f, -1f, 1f);
                onMove?.Invoke(p, vel);
                yield return null;
            }
        }

        static Vector3 CatmullPath(Vector3[] p, float u)
        {
            int n = p.Length - 1;
            float f = Mathf.Clamp01(u) * n;
            int i = Mathf.Min(n - 1, (int)f);
            float t = f - i;
            var p0 = p[Mathf.Max(0, i - 1)]; var p1 = p[i]; var p2 = p[i + 1]; var p3 = p[Mathf.Min(n, i + 2)];
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
        }

        GameObject Mesh(string name, MeshBuilder mb, Material mat, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.SetPositionAndRotation(pos, rot);
            junk.Add(go);
            return go;
        }

        void Sound(string name, Vector3 pos, float vol = 1f, float pitch = 1f) => AudioManager.Play(name, pos, vol, pitch, 1f, 3f, 120f);

        // ====================================================================== THE SCRIPT

        IEnumerator Script()
        {
            yield return ColdOpen();
            yield return Logo();
            yield return Sandbox();
            yield return Arena();
            yield return Silence();
            yield return OpenWorld();
            yield return Getaway();
            yield return Features();
            yield return Ending();
            yield return new WaitForSecondsRealtime(0.5f);
            Stop();
        }

        // ---------------------------------------------------------------- 0:00 cold open
        IEnumerator ColdOpen()
        {
            Lighting(TimeOfDay.Night, new Color(0.01f, 0.0f, 0.0f), 0.06f);
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }
            var v = TrailerSets.Void;
            var dropMb = new MeshBuilder { Color = new Color(0.55f, 0.0f, 0.02f), Gloss = 1f };
            dropMb.Sphere(Vector3.zero, new Vector3(0.035f, 0.05f, 0.035f), 10, 12);
            var drop = Mesh("Blood Drop", dropMb, Mats.Glow(new Color(0.6f, 0.02f, 0.02f), 0.6f), v + new Vector3(0, 5f, 0), Quaternion.identity);
            float dropFrom = 0.4f, dropTo = 4.4f;
            Shot(0f, 4.4f, u =>
            {
                float k = Mathf.Clamp01((T - dropFrom) / (dropTo - dropFrom));
                var p = v + new Vector3(0, Mathf.Lerp(5f, 0.03f, k * k), 0);
                if (drop != null) drop.transform.position = p;
                return (p + new Vector3(0.05f, 0.12f, -0.55f), p, 32f);
            });
            Black = 1f;
            StartCoroutine(FadeBlack(0f, 0f, 1.2f));
            Text("Every body tells a story.", 5.0f, 8.6f, Style.Thin, 0.62f, 52);
            yield return At(4.4f);
            Destroy(drop);
            BloodFX.Burst(v + Vector3.up * 0.02f, Vector3.up, 1.6f, new Color(0.55f, 0f, 0.02f));
            BloodFX.Mist(v + Vector3.up * 0.05f, Vector3.up, 0.6f);
            Decals.Blood(v + Vector3.up * 0.01f, Vector3.up, 0.6f, 0, null, 0.4f);
            Flash(new Color(0.8f, 0f, 0f), 0.55f, 2f);
            Ring(new Color(1f, 0.1f, 0.1f));
            Shot(4.4f, 9.3f, u => (v + new Vector3(0.6f - u * 0.5f, 0.35f + u * 0.9f, -1.6f - u * 1.2f), v + Vector3.up * 0.05f, 34f));
            yield return At(8.7f);
            StartCoroutine(FadeBlack(T, 1f, 0.6f));
        }

        IEnumerator FadeBlack(float unused, float target, float seconds)
        {
            float from = Black, t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < seconds && !stopped) { Black = Mathf.Lerp(from, target, (Time.unscaledTime - t0) / seconds); yield return null; }
            Black = target;
        }

        // ---------------------------------------------------------------- 0:09 logo
        IEnumerator Logo()
        {
            yield return At(B(5));
            Black = 1f;
            logoFrom = B(5); logoTo = 22.2f; logoHit = B(10);
            Text("SOLARECLIPSE PRODUCTIONS PRESENTS", B(7), B(10) - 0.15f, Style.Small, 0.78f, 24, new Color(1f, 0.75f, 0.45f));
            Text("GORE SANDBOX", B(10), 22.2f, Style.Logo, 0.62f, 150);
            yield return At(B(10));
            Impact(Color.white, 4);
            Shake(1f);
            Embers(new Rect(UI.VW * 0.2f, UI.VH * 0.3f, UI.VW * 0.6f, UI.VH * 0.4f), 160, new Color(1f, 0.55f, 0.15f), 380f);
            yield return At(21.6f);
            Embers(new Rect(UI.VW * 0.25f, UI.VH * 0.5f, UI.VW * 0.5f, UI.VH * 0.2f), 240, new Color(0.6f, 0.6f, 0.6f), 120f);
        }

        // ---------------------------------------------------------------- 0:19 sandbox
        IEnumerator Sandbox()
        {
            yield return At(22.0f);
            ClearScene();
            Lighting(TimeOfDay.Day, new Color(0.72f, 0.8f, 0.88f), 0.004f);
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            var c = TrailerSets.Lab;
            // Shot A: crane flyover
            Shot(22.0f, 25.4f, u => (c + Vector3.Lerp(new Vector3(-45, 28, -45), new Vector3(-5, 12, -18), E(u)), c + new Vector3(4, 4, 10), 50f));
            StartCoroutine(FadeBlack(0, 0f, 0.4f));
            Text("A physics sandbox.", 22.6f, 25.2f, Style.Slide, 0.74f, 54);
            // Three Goredolls doing their thing (set up now, filmed next).
            var d1 = Actor(c + new Vector3(-6, 0, 0), 160f, null, "Normal", null, 101);
            var d2 = Actor(c + new Vector3(-3, 0, 1.6f), 190f, null, "Normal", null, 202);
            var d3 = Actor(c + new Vector3(0.2f, 0, 0.4f), 120f, null, "Normal", null, 303);
            d1.Play(new TKey(0, TrailerPoses.Stand), new TKey(1.2f, TrailerPoses.NeckStretch), new TKey(2.6f, TrailerPoses.NeckStretch.With(new TPose().Set(PartType.Neck, 0, 0, -22).Set(PartType.Head, 0, 0, -18))), new TKey(4f, TrailerPoses.Stand));
            d2.Play(new TKey(0, TrailerPoses.Stand), new TKey(1.0f, TrailerPoses.LookHands), new TKey(3.5f, TrailerPoses.LookHands.With(new TPose().Set(PartType.HandL, 10).Set(PartType.HandR, 70))));
            d3.Pose(TrailerPoses.Stand);
            d3.Look(Expression.Curious);
            yield return At(25.4f);
            // Shot B: low dolly past them; the last one turns to the lens.
            Shot(25.4f, 28.6f, u => (c + new Vector3(-9f + u * 9f, 1.25f, -3.2f), c + new Vector3(-4f + u * 5f, 1.3f, 0.8f), 40f));
            Text("With real anatomy.", 26.2f, 28.5f, Style.Slide, 0.74f, 54);
            yield return At(26.9f);
            d3.LookAt = cam.transform.position;
            d3.Turn(200f, 1.2f);
            StartCoroutine(Follow(d3, 28.6f));
            yield return At(28.6f);
            // Shot C: off the diving board into the spikes
            var tip = TrailerSets.BoardTip != null ? TrailerSets.BoardTip.position : c + new Vector3(10, 10.2f, 16.6f);
            var diver = Actor(tip - new Vector3(0, 0.05f, 0.35f), 0f, null, "Normal", null, 404);
            diver.Pose(TrailerPoses.EdgeStand);
            diver.Look(Expression.Afraid);
            Shot(28.6f, B(17), u => (c + new Vector3(3.5f, 9f - u * 4f, 17.5f), Vector3.Lerp(tip, c + new Vector3(10, 1.2f, 18.5f), E(u)), 46f));
            yield return At(29.3f);
            diver.Play(new TKey(0, TrailerPoses.Fall));
            diver.MoveTo(tip + new Vector3(0, 0.1f, 0.6f), 0.25f);
            yield return At(29.55f);
            diver.Release(new Vector3(0, 0.5f, 1.2f), false);
            SlowMo(29.6f, 31.3f, 0.35f);
            yield return At(B(17) - 0.05f);
            Flash(Color.white, 0.9f, 6f);
        }

        IEnumerator Follow(TrailerActor a, float until)
        {
            while (T < until && a != null && cam != null) { a.LookAt = cam.transform.position; yield return null; }
        }

        // ---------------------------------------------------------------- 0:32 fight 1: Crimson Arena
        IEnumerator Arena()
        {
            yield return At(B(17));
            ClearScene();
            Lighting(TimeOfDay.BloodMoon, new Color(0.3f, 0.03f, 0.04f), 0.028f);
            Wash(new Color(0.8f, 0.02f, 0.05f), 0.1f, 0.12f);
            var c = TrailerSets.Arena + Vector3.up * 0.12f;
            var hero = Hero(c, 0f);
            hero.Pose(TrailerPoses.Stand);
            var foes = new List<TrailerActor>();
            for (int i = 0; i < 6; i++)
            {
                float a = 30f + i * 60f;
                var pos = c + Quaternion.Euler(0, a, 0) * Vector3.forward * 7.5f;
                var f = Actor(pos, a + 180f, null, "Normal", null, 1000 + i);
                f.Pose(TrailerPoses.Menace);
                f.Look(Expression.Angry);
                f.Face(c);
                foes.Add(f);
            }
            // Shot 1: overhead orbit; the fighter rolls his shoulders and draws.
            Shot(B(17), 35.6f, u => (Orbit(c, 11f - u * 2f, 13f - u * 4f, 200f + u * 50f), c + Vector3.up * 1f, 48f));
            yield return At(32.4f);
            hero.Play(new TKey(0, TrailerPoses.ShoulderRoll), new TKey(0.6f, TrailerPoses.ShoulderRoll.With(new TPose().Set(PartType.Neck, 0, 0, 18).Set(PartType.Head, -6, -10, 12))), new TKey(1.1f, TrailerPoses.Stand));
            for (int i = 0; i < foes.Count; i++)
            {
                var f = foes[i];
                float a = 30f + i * 60f + 25f;
                f.Gait = 0.35f; f.GaitSpeed = 2f;
                f.MoveTo(c + Quaternion.Euler(0, a, 0) * Vector3.forward * 6.2f, 3f);
                StartCoroutine(KeepFacing(f, hero, 36f));
            }
            yield return At(33.6f);
            hero.Arm("katana", true);
            Sound("katana_draw", hero.Chest, 1f);
            hero.Play(new TKey(0, TrailerPoses.DrawBlade), new TKey(0.7f, TrailerPoses.BladeGuard));
            yield return At(34.6f);
            FX.Sparks(hero.Part(PartType.HandR).position + Vector3.up * 0.4f, Vector3.up, 30, 6f);
            Flash(new Color(1f, 0.85f, 0.6f), 0.35f, 5f);
            Ring(new Color(1f, 0.6f, 0.3f));
            foreach (var f in foes) f.Gait = 0f;

            // Shot 2: the first one charges. Wind-up in slow motion, red impact frame, the arm comes off.
            var f0 = foes[0];
            yield return At(35.6f);
            Shot(35.6f, 40.3f, u => (c + hero.transform.rotation * new Vector3(-1.6f, 0.7f, -2.4f) + new Vector3(0, 0, u * 0.6f), Vector3.Lerp(hero.Chest, f0.Chest, 0.6f), 40f - u * 6f));
            hero.Face(f0.transform.position);
            f0.Gait = 1f; f0.GaitSpeed = 7f; f0.ArmsBusy = false;
            f0.Play(new TKey(0, TrailerPoses.Menace));
            f0.MoveTo(c + (f0.transform.position - c).normalized * 1.5f, 1.0f, 0f);
            f0.Look(Expression.Angry);
            yield return At(36.6f);
            hero.Play(new TKey(0, TrailerPoses.SlashWindup));
            Sound("v_argh", f0.Head, 0.9f);
            SlowMo(36.7f, 37.45f, 0.12f);
            yield return At(37.5f);
            f0.Gait = 0f;
            hero.Play(new TKey(0, TrailerPoses.SlashWindup), new TKey(0.12f, TrailerPoses.SlashFollow));
            Sound("katana_swing", hero.Chest, 1f);
            Impact(new Color(0.9f, 0.05f, 0.05f), 3);
            var cut = (f0.transform.position - hero.transform.position).normalized;
            f0.Slice(PartType.UpperArmR, Vector3.Cross(cut, Vector3.up).normalized, cut * 2f);
            f0.Hit(PartType.Chest, cut, 90f, DamageType.Slash, 600f);
            BloodFX.Spurt(f0.Chest, cut + Vector3.up * 0.4f, 2f);
            Sound("slice", f0.Chest, 1f);
            Text("SLICE.", 37.5f, 39.6f, Style.Slam, 0.5f, 140);
            SlowMo(37.55f, 38.6f, 0.3f);

            // Shot 3: two at once, spin attack, camera whips around in bullet time.
            yield return At(40.3f);
            var f1 = foes[1]; var f2 = foes[2];
            hero.Play(new TKey(0, TrailerPoses.BladeGuard));
            foreach (var f in new[] { f1, f2 }) { f.Gait = 1f; f.GaitSpeed = 6.5f; f.ArmsBusy = false; f.MoveTo(c + (f.transform.position - c).normalized * 1.6f, 1.3f, 0f); }
            Shot(40.3f, 45.0f, u => (Orbit(c, 4.2f, 1.6f + Mathf.Sin(u * 3f) * 0.4f, 90f + u * 300f), hero.Chest, 50f));
            yield return At(41.7f);
            f1.Gait = 0f; f2.Gait = 0f;
            f1.Pose(TrailerPoses.PipeUp); f2.Pose(TrailerPoses.Menace);
            hero.Play(new TKey(0, TrailerPoses.BladeOut));
            hero.Spin(540f, 0.9f);
            Sound("katana_swing", hero.Chest, 1f, 0.85f);
            SlowMo(41.9f, 43.6f, 0.22f);
            yield return At(42.35f);
            f1.Slice(PartType.ThighL, Vector3.up, (f1.transform.position - c).normalized * 3f);
            f1.Hit(PartType.Abdomen, (f1.transform.position - c).normalized, 80f, DamageType.Slash, 500f);
            BloodFX.Spurt(f1.Part(PartType.Abdomen).position, (f1.transform.position - c).normalized + Vector3.up, 2.2f);
            Flash(new Color(1f, 0.1f, 0.1f), 0.4f, 4f);
            yield return At(42.75f);
            f2.Sever(PartType.Head, (f2.transform.position - c).normalized, (f2.transform.position - c).normalized * 2f);
            BloodFX.Spurt(f2.Part(PartType.Neck).position, Vector3.up, 3f);
            Impact(new Color(0.9f, 0.05f, 0.05f), 2);
            Sound("dismember", f2.Chest, 1f);
            yield return At(43.7f);
            hero.Play(new TKey(0, TrailerPoses.BladeGuard));

            // Shot 4: dodge the pipe, counter-thrust through the chest.
            yield return At(45.0f);
            var f3 = foes[3];
            f3.Arm("pipe", true);
            f3.MoveTo(c + (hero.transform.position - c) + (f3.transform.position - hero.transform.position).normalized * 1.7f, 0.8f);
            hero.Face(f3.transform.position);
            StartCoroutine(KeepFacing(f3, hero, 49.5f));
            var side = Vector3.Cross((f3.transform.position - hero.transform.position).normalized, Vector3.up);
            Shot(45.0f, 49.7f, u => (Vector3.Lerp(hero.transform.position, f3.transform.position, 0.5f) + side * (4.2f - u * 0.6f) + Vector3.up * 1.2f, Vector3.Lerp(hero.Chest, f3.Chest, 0.5f), 42f));
            yield return At(45.6f);
            f3.Play(new TKey(0, TrailerPoses.PipeUp));
            yield return At(46.15f);
            f3.Play(new TKey(0, TrailerPoses.PipeSwing));
            Sound("whoosh_big", f3.Chest, 1f);
            hero.Play(new TKey(0, TrailerPoses.DodgeBack));
            hero.MoveTo(hero.transform.position - hero.transform.forward * 1.1f, 0.35f);
            SlowMo(46.2f, 47.0f, 0.2f);
            yield return At(47.1f);
            hero.Play(new TKey(0, TrailerPoses.Thrust));
            hero.MoveTo(hero.transform.position + hero.transform.forward * 1.0f, 0.15f);
            yield return At(47.35f);
            f3.Hit(PartType.Chest, hero.transform.forward, 120f, DamageType.Stab, 100f);
            BloodFX.Exit(f3.Chest + hero.transform.forward * 0.15f, hero.transform.forward, 2.5f);
            Sound("stab", f3.Chest, 1f);
            f3.Play(new TKey(0, TrailerPoses.Flinch));
            f3.Look(Expression.Pain);
            Impact(new Color(0.9f, 0.05f, 0.05f), 2);
            yield return At(48.5f);
            hero.Play(new TKey(0, TrailerPoses.Thrust), new TKey(0.25f, TrailerPoses.BladeGuard));
            hero.MoveTo(hero.transform.position - hero.transform.forward * 0.8f, 0.3f);
            BloodFX.Spurt(f3.Chest, hero.transform.forward * -1f, 1.5f);
            yield return At(48.8f);
            f3.Release(Vector3.down * 0.5f);

            // Shot 5: shotgun, point blank.
            yield return At(49.7f);
            var f4 = foes[4];
            hero.Arm("shotgun", false);
            hero.Play(new TKey(0, TrailerPoses.AimRifle));
            hero.Face(f4.transform.position);
            f4.Gait = 1f; f4.GaitSpeed = 7f; f4.ArmsBusy = false; f4.Play(new TKey(0, TrailerPoses.Scream));
            var toHero = (hero.transform.position - f4.transform.position).normalized;
            f4.MoveTo(hero.transform.position - toHero * 1.3f, 1.5f, 0f);
            StartCoroutine(KeepFacing(f4, hero, 51.3f));
            Shot(49.7f, 53.4f, u => (hero.transform.position + Quaternion.LookRotation(-toHero) * new Vector3(0.9f, 1.55f, -1.4f) + Vector3.up * u * 0.2f, Vector3.Lerp(hero.Muzzle, f4.Chest, 0.6f), 36f + u * 10f));
            yield return At(51.3f);
            f4.Gait = 0f;
            Shoot(hero, f4.Chest, "gun_ar17", 1f, false);
            FX.Flash(hero.Muzzle, new Color(1f, 0.6f, 0.2f), 14f, 9f, 0.12f);
            Flash(new Color(1f, 0.55f, 0.15f), 0.6f, 3f);
            Impact(Color.white, 2);
            hero.Play(new TKey(0, TrailerPoses.Recoil), new TKey(0.35f, TrailerPoses.AimRifle));
            var blast = (f4.Chest - hero.Muzzle).normalized;
            for (int p = 0; p < 6; p++) f4.Hit(p % 2 == 0 ? PartType.Chest : PartType.Abdomen, (blast + Random.insideUnitSphere * 0.15f).normalized, 40f, DamageType.Buckshot, 300f);
            f4.Gib(PartType.Head, blast, blast * 15f + Vector3.up * 4f);
            BloodFX.Mist(f4.Chest, blast, 3f);
            Casing(hero.Muzzle - blast * 0.5f, Quaternion.LookRotation(blast) * new Vector3(2f, 2f, -0.5f));
            Text("SMASH.", 51.3f, 53.3f, Style.Slam, 0.5f, 140);
            SlowMo(51.35f, 52.9f, 0.15f);

            // Shot 6: the big one. Sledgehammer, freeze, white, black, impact.
            yield return At(53.4f);
            var boss = Actor(c + new Vector3(0, 0, 4f), 180f, Look("Brute", new Color(0.2f, 0.22f, 0.18f), new Color(0.15f, 0.15f, 0.12f), false, 1.18f, 1.4f, 2), "Normal~a3", null, 5555);
            boss.Pose(TrailerPoses.Scream);
            boss.Look(Expression.Angry);
            hero.Arm("sledgehammer", true);
            hero.Face(boss.transform.position);
            hero.MoveTo(boss.transform.position - (boss.transform.position - hero.transform.position).normalized * 1.6f, 0.6f);
            Sound("v_scream", boss.Head, 1f, 0.8f);
            Shot(53.4f, B(31), u => (hero.transform.position + hero.transform.rotation * new Vector3(1.4f, 0.35f, -1.1f), hero.Chest + Vector3.up * (0.6f + u * 0.8f), 54f));
            yield return At(54.5f);
            hero.Play(new TKey(0, TrailerPoses.HammerUp));
            Sound("sledge_swing", hero.Chest, 1f, 0.8f);
            SlowMo(54.55f, 56.1f, 0.25f);
            yield return At(56.1f);
            impactCol = Color.white; impactFrames = 0;
            whiteUntil = 56.35f; blackUntil = 56.65f;
            yield return At(56.65f);
            hero.Play(new TKey(0, TrailerPoses.HammerDown));
            boss.Gib(PartType.Head, Vector3.down, Vector3.down * 3f + hero.transform.forward * 2f);
            boss.Hit(PartType.Chest, Vector3.down, 150f, DamageType.Blunt, 900f);
            Sound("sledge_flesh", boss.Chest, 1f); Sound("bone_crack", boss.Chest, 1f);
            FX.Dust(boss.transform.position, 3f, new Color(0.7f, 0.5f, 0.38f), 40);
            Ring(new Color(1f, 0.3f, 0.2f));
            Impact(new Color(0.9f, 0.05f, 0.05f), 3);
            Shake(1f);
            Text("SHATTER.", 56.65f, 57.4f, Style.Slam, 0.42f, 130);
            Text("SUPER ULTRA GORE", 57.25f, B(31), Style.Slam, 0.58f, 120, new Color(1f, 0.15f, 0.12f));
            foes[5].Play(new TKey(0, TrailerPoses.Flinch));
        }

        IEnumerator KeepFacing(TrailerActor a, TrailerActor target, float until)
        {
            while (T < until && a != null && target != null && a.Puppet) { a.Face(target.transform.position); yield return null; }
        }

        float whiteUntil = -1f, blackUntil = -1f;

        // ---------------------------------------------------------------- 0:58 silence
        IEnumerator Silence()
        {
            yield return At(B(31));
            Black = 1f;
            Rules.TimeScale = 1f;
            Text("But that's not all.", 58.6f, 59.85f, Style.Small, 0.5f, 30);
            yield return null;
        }

        // ---------------------------------------------------------------- 1:00 open world
        IEnumerator OpenWorld()
        {
            yield return At(B(32) - 0.1f);
            ClearScene();
            Lighting(TimeOfDay.Sunset, new Color(0.85f, 0.5f, 0.38f), 0.008f);
            Wash(new Color(0.9f, 0.2f, 0.6f), 0.06f, 0.1f);
            var c = TrailerSets.City;
            // Shot 1: supercar drift toward the lens.
            var car = Car("c_super", "u_paint_black", "u_neon_pink", c + new Vector3(37, 0.05f, -60), 0f);
            var path = new[] { c + new Vector3(37, 0.05f, -60), c + new Vector3(37, 0.05f, -18), c + new Vector3(33, 0.05f, -2), c + new Vector3(22, 0.05f, 2.5f), c + new Vector3(0, 0.05f, 3f), c + new Vector3(-40, 0.05f, 3f) };
            StartCoroutine(Drive(car, path, B(32), 64.2f, 38f, (p, vel) =>
            {
                if (vel.magnitude > 8f && Random.value < 0.7f)
                {
                    var back = p - car.transform.forward * 2f;
                    FX.Smoke(back + Vector3.up * 0.2f, Vector3.up * 0.6f + Random.insideUnitSphere * 0.4f, 1.2f, new Color(0.75f, 0.75f, 0.78f, 0.6f), 2.5f);
                    if (Random.value < 0.3f) FX.Sparks(back, Vector3.up, 4, 6f);
                }
            }));
            Shot(B(32), 64.2f, u =>
            {
                var cp = c + new Vector3(12f, 0.45f, -3.2f);
                var look = car != null ? car.transform.position + Vector3.up * 0.6f : c;
                smoothLook = u < 0.02f ? look : Vector3.Lerp(smoothLook, look, 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime));
                return (cp, smoothLook, 46f);
            });
            Black = 0f;
            Impact(Color.white, 2);
            Text("OPEN WORLD", B(32), 62.6f, Style.Slam, 0.5f, 150);
            Text("CARS", 62.8f, 64.1f, Style.Slam, 0.72f, 110, new Color(1f, 0.4f, 0.85f));
            yield return At(64.2f);
            // Shot 2: slow push to the bank.
            Shot(64.2f, 68.0f, u => (c + Vector3.Lerp(new Vector3(-4, 1.4f, -24), new Vector3(-4, 2.6f, -4), E(u)), c + new Vector3(-4, 6f + u * 2f, 12.5f), 50f));
            Text("HEISTS", 66.0f, 67.9f, Style.Slam, 0.72f, 120, new Color(1f, 0.82f, 0.3f));
            yield return At(68.0f);
            // Shot 3: inside, alarm, vault opens.
            var b = TrailerSets.Bank;
            Lighting(TimeOfDay.Night, new Color(0.08f, 0.02f, 0.02f), 0.01f);
            Shot(68.0f, 72.6f, u => (b + Vector3.Lerp(new Vector3(-5, 2.2f, -8), new Vector3(-2, 2.8f, 1f), E(u)), b + new Vector3(0, 2.6f, 11f), 52f));
            yield return At(68.6f);
            alarm = true; alarmFrom = T;
            StartCoroutine(AlarmWash(80.6f));
            for (int i = 0; i < 4; i++) StartCoroutine(SoundAt("buzzer", b + new Vector3(0, 6, 8), 68.6f + i * 0.94f, 0.9f));
            yield return At(69.0f);
            StartCoroutine(OpenVault(69.0f, 71.6f));
            yield return At(71.2f);
            for (int i = 0; i < 6; i++) FX.Flash(b + new Vector3(Random.Range(-5f, 5f), 1.5f, Random.Range(13f, 16f)), new Color(1f, 0.85f, 0.4f), 5f, 3f, 0.4f);
            Flash(new Color(1f, 0.8f, 0.3f), 0.25f, 2f);
            yield return At(72.6f);
            // Shot 4: the crew walks out in slow motion, money drifting.
            Lighting(TimeOfDay.Night, new Color(0.1f, 0.03f, 0.03f), 0.008f);
            var crew = new[]
            {
                Actor(b + new Vector3(0, 0, 13f), 180f, Look("Skull", new Color(0.07f, 0.07f, 0.08f), new Color(0.09f, 0.09f, 0.1f), true, 1.06f, 1.18f, 1), "Normal", "m_skull", 777),
                Actor(b + new Vector3(-1.6f, 0, 14f), 180f, Look("Clown", new Color(0.55f, 0.1f, 0.1f), new Color(0.12f, 0.12f, 0.14f)), "Normal", "m_clown", 888),
                Actor(b + new Vector3(1.6f, 0, 14f), 180f, Look("Hockey", new Color(0.2f, 0.25f, 0.4f), new Color(0.15f, 0.15f, 0.18f)), "Normal", "m_hockey", 999),
            };
            string[] guns = { "ak47", "glock", "m4a1" };
            for (int i = 0; i < crew.Length; i++)
            {
                var a = crew[i];
                a.Arm(guns[i], false);
                a.Play(new TKey(0, i == 1 ? TrailerPoses.Swagger : TrailerPoses.CrewWalk));
                a.Gait = 0.55f; a.GaitSpeed = 2.4f; a.ArmsBusy = true; a.Breathe = 0.5f;
                a.MoveTo(a.transform.position + new Vector3(0, 0, -13f), 7.5f, 0f);
            }
            Money(b + new Vector3(0, 0, 3f), 60);
            SlowMo(72.7f, 79.8f, 0.4f);
            Shot(72.6f, B(43), u => (b + new Vector3(0.3f, 1.3f, -6.5f + u * 1.2f), Vector3.Lerp(crew[0].Head, crew[0].Chest, 0.4f) + new Vector3(0, 0, -0.5f), 44f - u * 6f));
            Text("100+ ITEMS", 74.2f, 76.6f, Style.Slam, 0.72f, 110, new Color(0.45f, 1f, 0.5f));
            Text("POLICE", 78.6f, 80.5f, Style.Slam, 0.72f, 120, new Color(0.4f, 0.6f, 1f));
            yield return At(78.6f);
            Sound("siren", b + new Vector3(0, 2, -14), 0.8f);
        }

        IEnumerator AlarmWash(float until)
        {
            while (T < until && !stopped) { Wash(new Color(1f, 0.03f, 0.03f), Mathf.Repeat(T - alarmFrom, 0.5f) < 0.25f ? 0.2f : 0.04f); yield return null; }
        }

        IEnumerator SoundAt(string name, Vector3 pos, float at, float vol)
        {
            yield return At(at);
            Sound(name, pos, vol);
        }

        IEnumerator OpenVault(float from, float to)
        {
            Sound("metal_heavy", TrailerSets.Bank + new Vector3(0, 3, 10), 1f, 0.6f);
            while (T < to && !stopped)
            {
                float u = E(Mathf.Clamp01((T - from) / (to - from)));
                if (TrailerSets.VaultDoor != null) TrailerSets.VaultDoor.localRotation = Quaternion.Euler(0, -105f * u, 0);
                yield return null;
            }
        }

        // ---------------------------------------------------------------- 1:20 fight 2: the getaway
        IEnumerator Getaway()
        {
            yield return At(B(43) - 0.05f);
            ClearScene();
            Lighting(TimeOfDay.Night, new Color(0.08f, 0.06f, 0.12f), 0.012f);
            policePulse = true;
            var c = TrailerSets.City;
            // Police cars skid in.
            var cops = new List<OWCar>
            {
                Car("c_police", "", "", c + new Vector3(-70, 0.05f, -3), 90f),
                Car("c_police", "", "", c + new Vector3(70, 0.05f, -2), -90f),
                Car("c_police", "", "", c + new Vector3(37, 0.05f, -70), 0f),
            };
            StartCoroutine(Drive(cops[0], new[] { c + new Vector3(-70, 0.05f, -3), c + new Vector3(-30, 0.05f, -3), c + new Vector3(-16, 0.05f, -2) }, B(43), 82.4f, 80f));
            StartCoroutine(Drive(cops[1], new[] { c + new Vector3(70, 0.05f, -2), c + new Vector3(30, 0.05f, -3), c + new Vector3(12, 0.05f, -1.5f) }, B(43), 82.6f, 80f));
            StartCoroutine(Drive(cops[2], new[] { c + new Vector3(37, 0.05f, -70), c + new Vector3(37, 0.05f, -20), c + new Vector3(33, 0.05f, -4), c + new Vector3(28, 0.05f, -1) }, B(43), 82.9f, 60f));
            StartCoroutine(SoundAt("siren", c + new Vector3(0, 2, -6), B(43), 1f));
            // The crew bursts out of the bank.
            var bankDoor = c + new Vector3(-4, 0.5f, 11.6f);
            var skull = Hero(bankDoor + new Vector3(0, 0, -0.5f), 180f);
            var clown = Actor(bankDoor + new Vector3(-1.6f, 0, -0.4f), 180f, Look("Clown", new Color(0.55f, 0.1f, 0.1f), new Color(0.12f, 0.12f, 0.14f)), "Normal", "m_clown", 888);
            var hockey = Actor(bankDoor + new Vector3(1.6f, 0, -0.4f), 180f, Look("Hockey", new Color(0.2f, 0.25f, 0.4f), new Color(0.15f, 0.15f, 0.18f)), "Normal", "m_hockey", 999);
            skull.Arm("rpg", false); clown.Arm("ak47", false); hockey.Arm("m4a1", false);
            foreach (var a in new[] { skull, clown, hockey }) { a.Gait = 1f; a.GaitSpeed = 6f; a.ArmsBusy = true; a.Play(new TKey(0, TrailerPoses.CrewWalk)); var tp = a.transform.position; a.MoveTo(new Vector3(tp.x, c.y + 0.02f, c.z + 5f), 1.6f, 0f); }
            Shot(B(43), 84.4f, u => (c + Vector3.Lerp(new Vector3(18, 2.6f, -14), new Vector3(14, 2.2f, -12), u), c + new Vector3(-4, 1.6f, 4f), 52f));
            Text("PLAY WITH FRIENDS.", 81.3f, 84.2f, Style.Slam, 0.74f, 96);
            Impact(Color.white, 2);
            yield return At(82.9f);
            // SWAT pile out of the police cars.
            var swat = new List<TrailerActor>();
            Vector3[] swatPos = { c + new Vector3(-15, 0.02f, -5.5f), c + new Vector3(-18, 0.02f, -5f), c + new Vector3(11, 0.02f, -4.5f), c + new Vector3(14, 0.02f, -4.5f), c + new Vector3(27, 0.02f, -4f) };
            for (int i = 0; i < swatPos.Length; i++)
            {
                var s = Actor(swatPos[i], 0f, null, "Soldier", null, 3000 + i);
                s.Play(new TKey(0, TrailerPoses.AimRifle));
                s.Face(c + new Vector3(-4, 0, 6));
                s.Look(Expression.Angry);
                swat.Add(s);
            }
            foreach (var a in new[] { skull, clown, hockey }) a.Gait = 0f;
            skull.Play(new TKey(0, TrailerPoses.AimRifle)); hockey.Play(new TKey(0, TrailerPoses.AimRifle));
            clown.Play(new TKey(0, TrailerPoses.AimPistol));
            foreach (var a in new[] { skull, clown, hockey }) a.Face(c + new Vector3(0, 0, -5));

            // Shot 2: bullet time gunfight; the clown slides into cover firing.
            yield return At(84.4f);
            SlowMo(84.5f, 88.7f, 0.08f);
            clown.Play(new TKey(0, TrailerPoses.CoverFire));
            clown.MoveTo(clown.transform.position + new Vector3(2.5f, -0.1f, -1.2f), 1.2f);
            Shot(84.4f, 89.0f, u => (Orbit(clown.transform.position + Vector3.up * 0.9f, 3.2f, 0.5f, 150f + u * 120f), clown.Chest, 46f));
            StartCoroutine(Gunfight(new[] { clown, hockey }, swat, 84.45f, 88.9f));
            yield return At(86.4f);
            if (swat.Count > 1 && swat[1].Puppet)
            {
                var s = swat[1];
                var dir = (s.Chest - clown.Chest).normalized;
                s.Hit(PartType.Head, dir, 200f, DamageType.Bullet, 300f);
                BloodFX.Mist(s.Head, dir, 2.5f);
                s.Release(dir * 2f);
                Flash(new Color(1f, 0.1f, 0.1f), 0.35f, 3f);
            }
            yield return At(89.0f);
            // Shot 3: rocket into the middle police car, it flips over the camera.
            var target = cops[1];
            skull.Face(target.transform.position);
            skull.Play(new TKey(0, TrailerPoses.AimRifle.With(new TPose().Set(PartType.UpperArmR, -95, 0, -30).Set(PartType.ForearmR, -110).Set(PartType.Head, 10, -20))));
            var tpos = target.transform.position;
            Shot(89.0f, 92.8f, u => (tpos + new Vector3(-9f + u * 2f, 0.6f, -7f), Vector3.Lerp(skull.Chest, tpos + Vector3.up * (u > 0.4f ? 4f * (u - 0.4f) * 3f : 0f), Mathf.Clamp01(u * 2.2f)), 56f));
            yield return At(89.6f);
            Sound("rocket_fire", skull.Muzzle, 1f);
            FX.Flash(skull.Muzzle, new Color(1f, 0.6f, 0.2f), 10f, 6f, 0.15f);
            var rmb = new MeshBuilder { Color = Color.white };
            rmb.Capsule(new Vector3(0, 0, -0.25f), new Vector3(0, 0, 0.25f), 0.06f, 0.06f, 8, 2);
            var rocket = Mesh("Rocket", rmb, Mats.Glow(new Color(1f, 0.7f, 0.3f), 3f), skull.Muzzle, Quaternion.LookRotation(tpos - skull.Muzzle));
            float rFrom = T, rTo = T + 0.55f;
            var rStart = skull.Muzzle;
            while (T < rTo && !stopped)
            {
                float k = (T - rFrom) / (rTo - rFrom);
                rocket.transform.position = Vector3.Lerp(rStart, tpos + Vector3.up * 0.8f, k);
                FX.Smoke(rocket.transform.position, Random.insideUnitSphere * 0.3f, 0.6f, new Color(0.7f, 0.7f, 0.72f, 0.8f), 2.5f);
                yield return null;
            }
            Destroy(rocket);
            var rb = target.GetComponent<Rigidbody>();
            target.DriverId = 255;
            if (rb != null) { rb.isKinematic = false; rb.AddForce(Vector3.up * 13f + Vector3.left * 3f, ForceMode.VelocityChange); rb.AddTorque(new Vector3(7f, 1f, 4f), ForceMode.VelocityChange); }
            target.Explode();
            Flash(new Color(1f, 0.5f, 0.1f), 0.85f, 2f);
            Impact(Color.white, 2);
            Ring(new Color(1f, 0.6f, 0.2f));
            Shake(1f);
            SlowMo(T + 0.05f, T + 0.9f, 0.3f);
            yield return At(92.8f);
            // Shot 4: the supercar ploughs through two officers.
            var super = Car("c_super", "u_paint_black", "u_neon_pink", c + new Vector3(-60, 0.05f, -4.5f), 90f);
            var hit = new List<TrailerActor>();
            foreach (var s in swat) if (s != null && s.Puppet) hit.Add(s);
            var victims = hit.GetRange(0, Mathf.Min(2, hit.Count));
            for (int i = 0; i < victims.Count; i++) { victims[i].MoveTo(c + new Vector3(-12 + i * 3.5f, 0.02f, -4.6f), 0.3f); victims[i].Face(c + new Vector3(-30, 0, -4.5f)); victims[i].Play(new TKey(0, TrailerPoses.Flinch)); victims[i].Look(Expression.Afraid); }
            StartCoroutine(Drive(super, new[] { c + new Vector3(-60, 0.05f, -4.5f), c + new Vector3(-20, 0.05f, -4.6f), c + new Vector3(10, 0.05f, -4.6f), c + new Vector3(40, 0.05f, -4f) }, 92.8f, 96.0f, 0f, (p, vel) =>
            {
                foreach (var v in victims)
                    if (v != null && v.Puppet && Vector3.Distance(MathX.Flat(v.transform.position), MathX.Flat(p)) < 2.6f)
                    {
                        v.Release(vel * 0.75f + Vector3.up * 6f);
                        v.Hit(PartType.Pelvis, vel.normalized, 120f, DamageType.Blunt, 800f);
                        Sound("body_thud", v.Chest, 1f); Sound("bone_crack", v.Chest, 1f);
                        Impact(new Color(0.9f, 0.05f, 0.05f), 2);
                    }
            }));
            Shot(92.8f, 96.0f, u => (c + new Vector3(-8f + u * 6f, 1.1f, -12f), (super != null ? super.transform.position : c) + Vector3.up * 0.8f, 50f));
            yield return At(96.0f);
            // Shot 5: docks knife fight, cut on every beat.
            ClearScene();
            Lighting(TimeOfDay.Night, new Color(0.05f, 0.09f, 0.18f), 0.03f);
            Wash(new Color(0.1f, 0.35f, 1f), 0.1f, 0.08f);
            var d = TrailerSets.Docks + new Vector3(1.5f, 0, -16f);
            var me = Hero(d + new Vector3(-1.1f, 0, 0), 90f);
            var gang = Actor(d + new Vector3(1.1f, 0, 0), -90f, Look("Gang", new Color(0.55f, 0.08f, 0.08f), new Color(0.1f, 0.1f, 0.1f), false, 1.02f, 1.1f, 2), "Normal", "m_bandana", 4242);
            me.Arm("knife", true); gang.Arm("machete", true);
            me.Pose(TrailerPoses.KnifeGuard); gang.Pose(TrailerPoses.KnifeGuard); gang.Look(Expression.Angry);
            float beat = TrailerMusic.Beat;
            Vector3 Mid() => Vector3.Lerp(me.Chest, gang.Chest, 0.5f);
            Shot(96.0f, 96.0f + beat * 2, u => (d + new Vector3(0, 1.4f, -3.4f), Mid(), 44f));
            yield return At(96.0f + beat * 2);
            gang.Play(new TKey(0, TrailerPoses.Thrust)); gang.MoveTo(gang.transform.position + Vector3.left * 0.4f, 0.15f);
            me.Play(new TKey(0, TrailerPoses.Block));
            Shot(T, T + beat * 2, u => (d + new Vector3(-2.2f, 1.2f, 1.6f), Mid(), 38f));
            yield return At(96.0f + beat * 2 + 0.1f);
            FX.Sparks(Mid(), Vector3.up, 24, 7f); Sound("katana_clang", Mid(), 1f); Flash(new Color(0.6f, 0.8f, 1f), 0.3f, 6f);
            yield return At(96.0f + beat * 4);
            me.Play(new TKey(0, TrailerPoses.Elbow)); me.MoveTo(me.transform.position + Vector3.right * 0.3f, 0.12f);
            Shot(T, T + beat * 2, u => (d + new Vector3(2.4f, 1.0f, -1.2f), gang.Head, 34f));
            yield return At(96.0f + beat * 4 + 0.08f);
            gang.Play(new TKey(0, TrailerPoses.Flinch)); gang.Look(Expression.Pain);
            Sound("punch_hit", gang.Head, 1f); BloodFX.Mist(gang.Head, Vector3.right, 0.8f); Shake(0.5f);
            yield return At(96.0f + beat * 6);
            gang.Play(new TKey(0, TrailerPoses.PipeSwing));
            me.Play(new TKey(0, TrailerPoses.Block));
            Shot(T, T + beat * 2, u => (d + new Vector3(0.2f, 0.5f, -2.0f), Mid() + Vector3.up * 0.2f, 40f));
            yield return At(96.0f + beat * 6 + 0.1f);
            FX.Sparks(Mid() + Vector3.up * 0.3f, Vector3.up, 30, 8f); Sound("katana_clang", Mid(), 1f, 1.2f); Flash(new Color(0.6f, 0.8f, 1f), 0.3f, 6f);
            yield return At(99.2f);
            SlowMo(99.2f, 100.25f, 0.18f);
            me.Play(new TKey(0, TrailerPoses.ThroatSlash)); me.MoveTo(me.transform.position + Vector3.right * 0.35f, 0.2f);
            Shot(99.2f, 100.3f, u => (Orbit(gang.Head, 1.6f, 0.0f, -60f + u * 40f), gang.Head, 34f));
            yield return At(99.55f);
            gang.Hit(PartType.Neck, Vector3.right, 60f, DamageType.Slash, 100f);
            BloodFX.Spurt(gang.Part(PartType.Neck).position, Vector3.right + Vector3.up * 0.3f, 3f);
            gang.Sever(PartType.Head, Vector3.right, Vector3.right * 1.5f);
            Sound("slice", gang.Head, 1f);
            Impact(new Color(0.9f, 0.05f, 0.05f), 2);
            yield return At(100.3f);
            // Shot 6: the boat leaves, the city burns.
            Wash(new Color(1f, 0.45f, 0.1f), 0.12f, 0.1f);
            var boatMb = new MeshBuilder { Color = new Color(0.92f, 0.92f, 0.95f), Gloss = 0.6f };
            boatMb.BevelBox(new Vector3(0, 0, 0), new Vector3(3.6f, 1.3f, 9f), 0.3f);
            boatMb.SetColor(new Color(0.1f, 0.12f, 0.15f), 0.9f);
            boatMb.BevelBox(new Vector3(0, 1.1f, -1f), new Vector3(2.6f, 1f, 3f), 0.2f);
            var boat = Mesh("Getaway Boat", boatMb, Mats.LitMetal, TrailerSets.Docks + new Vector3(10, -1.2f, 28), Quaternion.identity);
            var fires = new List<Vector3>();
            for (int i = 0; i < 6; i++)
            {
                var fp = TrailerSets.Docks + new Vector3(-60 + i * 22, 0, -70);
                fires.Add(fp);
                var l = Keep(new GameObject("Fire Glow").AddComponent<Light>());
                l.transform.position = fp + Vector3.up * 6; l.type = LightType.Point; l.color = new Color(1f, 0.45f, 0.1f); l.range = 60f; l.intensity = 3f;
            }
            Shot(100.3f, B(55), u => (TrailerSets.Docks + new Vector3(-6, 3.5f, 12f), boat.transform.position + new Vector3(0, 1, 0), 46f));
            starsFrom = 100.6f; starsTo = B(55);
            Text("ESCAPE ALIVE.", 100.6f, B(55) - 0.1f, Style.Slam, 0.7f, 110);
            while (T < B(55) && !stopped)
            {
                boat.transform.position += new Vector3(0, 0, 14f) * Time.deltaTime;
                if (Random.value < 0.5f) FX.Smoke(boat.transform.position + new Vector3(Random.Range(-1f, 1f), -0.4f, -4.6f), Vector3.up * 0.5f + Random.insideUnitSphere * 0.5f, 1f, new Color(0.85f, 0.9f, 0.95f, 0.6f), 1.6f);
                foreach (var fp in fires) if (Random.value < 0.3f) FX.Fire(fp + new Vector3(Random.Range(-6f, 6f), Random.Range(0f, 8f), 0), Vector3.up * 2f, 3f);
                yield return null;
            }
        }

        IEnumerator Gunfight(TrailerActor[] crew, List<TrailerActor> swat, float from, float to)
        {
            float next = from;
            int k = 0;
            while (T < to && !stopped)
            {
                if (T >= next)
                {
                    next = T + 0.09f;
                    var shooter = k % 3 == 2 ? swat[k % swat.Count] : crew[k % crew.Length];
                    var target = shooter == null ? null : (k % 3 == 2 ? crew[(k / 3) % crew.Length] : swat[(k / 2) % swat.Count]);
                    if (shooter != null && target != null && shooter.Puppet)
                        Shoot(shooter, target.Chest + Random.insideUnitSphere * 0.6f, shooter.Weapon != null && k % 3 != 2 && shooter == crew[0] ? "gun_glock" : "gun_ar15", 0.7f);
                    k++;
                }
                yield return null;
            }
        }

        // ---------------------------------------------------------------- 1:43 features
        IEnumerator Features()
        {
            yield return At(B(55));
            ClearScene();
            starsFrom = -1f;
            float bar = TrailerMusic.Bar;
            // 1: story mode (purple)
            Lighting(TimeOfDay.Night, new Color(0.08f, 0.04f, 0.15f), 0.02f);
            Wash(new Color(0.6f, 0.2f, 1f), 0.16f);
            var d = TrailerSets.Docks + new Vector3(1.5f, 0, -16f);
            var hero = Hero(d + new Vector3(-1f, 0, 0), 90f);
            var marcus = Actor(d + new Vector3(1.2f, 0, 0.2f), -90f, Look("Big Marcus", new Color(0.85f, 0.65f, 0.15f), new Color(0.1f, 0.1f, 0.12f), false, 1.15f, 1.4f, 2, new Color(0.45f, 0.3f, 0.22f)), "Normal", null, 1212);
            hero.Pose(TrailerPoses.Stand); marcus.Play(new TKey(0, TrailerPoses.Stand), new TKey(0.6f, TrailerPoses.Stand.With(new TPose().Set(PartType.UpperArmR, -50, 0, 20).Set(PartType.ForearmR, -60))));
            marcus.LookAt = hero.Head; hero.LookAt = marcus.Head;
            Shot(B(55), B(56), u => (d + new Vector3(-2.6f + u * 0.4f, 1.6f, -1.6f), marcus.Head, 36f));
            feature = 0; featureFrom = B(55);
            Text("STORY MODE  ·  CUTSCENES  ·  ENDINGS", B(55), B(56), Style.Card, 0.16f, 48);
            yield return At(B(56));
            // 2: weapon wall (cyan)
            Lighting(TimeOfDay.Night, new Color(0.02f, 0.05f, 0.07f), 0.01f);
            Wash(new Color(0.1f, 0.8f, 1f), 0.14f);
            var r = TrailerSets.Rack;
            Shot(B(56), B(57), u => (r + new Vector3(-8f + u * 16f, 3.5f, 0.8f), r + new Vector3(-6f + u * 12f, 4f, 6f), 50f));
            feature = 1;
            Text("30 WEAPONS  ·  MOD YOUR OWN", B(56), B(57), Style.Card, 0.16f, 48);
            yield return At(B(57));
            // 3: VR (green): first-person hands holding a gun
            Lighting(TimeOfDay.Day, new Color(0.6f, 0.85f, 0.7f), 0.006f);
            Wash(new Color(0.2f, 1f, 0.45f), 0.14f);
            var lab = TrailerSets.Lab;
            var vr = Hero(lab + new Vector3(0, 0, -10), 0f);
            vr.Arm("glock", false);
            vr.Pose(TrailerPoses.AimPistol);
            Shot(B(57), B(58), u =>
            {
                var head = vr.Part(PartType.Head);
                var pos = head.position + vr.transform.forward * 0.12f + Vector3.up * 0.06f + vr.transform.right * Mathf.Sin(u * 6f) * 0.03f;
                return (pos, pos + vr.transform.forward * 3f + Vector3.down * 0.25f, 72f);
            });
            vr.Part(PartType.Head).gameObject.SetActive(false);
            feature = 2;
            Text("VR ON PC", B(57), B(58), Style.Card, 0.16f, 56);
            yield return At(B(57) + 0.9f);
            Shoot(vr, vr.Muzzle + vr.MuzzleDir * 20f, "gun_glock", 0.9f);
            vr.Play(new TKey(0, TrailerPoses.AimPistol.With(new TPose().Set(PartType.UpperArmR, -100, 0, -8))), new TKey(0.2f, TrailerPoses.AimPistol));
            yield return At(B(58));
            // 4: phone + PC (gold)
            Lighting(TimeOfDay.Sunset, new Color(0.85f, 0.5f, 0.38f), 0.008f);
            Wash(new Color(1f, 0.75f, 0.2f), 0.18f);
            var c = TrailerSets.City;
            Shot(B(58), B(59), u => (c + new Vector3(-30 + u * 10f, 18f, -40f), c + new Vector3(0, 6, 20), 50f));
            feature = 3;
            Text("ANDROID + PC", B(58), B(59), Style.Card, 0.16f, 56);
            yield return At(B(59));
            feature = -1;
        }

        // ---------------------------------------------------------------- 1:50 ending
        IEnumerator Ending()
        {
            yield return At(B(59) - 0.05f);
            ClearScene();
            Lighting(TimeOfDay.Night, new Color(0.03f, 0.03f, 0.06f), 0.01f);
            var c = TrailerSets.City;
            var bmb = new MeshBuilder { Color = new Color(0.8f, 0.6f, 0.25f), Gloss = 0.95f };
            bmb.Cylinder(new Vector3(0, 0, -0.012f), new Vector3(0, 0, 0.006f), 0.0045f, 0.0045f, 16);
            bmb.SetColor(new Color(0.75f, 0.42f, 0.25f), 0.95f);
            bmb.Capsule(new Vector3(0, 0, 0.004f), new Vector3(0, 0, 0.012f), 0.0045f, 0.0012f, 16, 4);
            var camPos = c + new Vector3(0, 9f, -20f);
            var dirToCam = Vector3.back;
            var bullet = Mesh("Bullet", bmb, Mats.LitMetal, camPos - dirToCam * 6f, Quaternion.LookRotation(dirToCam));
            bullet.transform.localScale = Vector3.one * 5f;
            var glint = Keep(new GameObject("Bullet Light").AddComponent<Light>());
            glint.type = LightType.Point; glint.color = new Color(1f, 0.85f, 0.6f); glint.range = 0.6f; glint.intensity = 3f;
            float from = B(59), to = 114.2f;
            Shot(from, to, u => (camPos, camPos - dirToCam * 2f, 20f));
            Black = 0f;
            while (T < to && !stopped)
            {
                float u = Mathf.Clamp01((T - from) / (to - from));
                var p = camPos - dirToCam * Mathf.Lerp(4f, 0.12f, u * u);
                bullet.transform.position = p;
                bullet.transform.rotation = Quaternion.LookRotation(dirToCam) * Quaternion.Euler(0, 0, T * 400f);
                glint.transform.position = p + Vector3.up * 0.05f;
                yield return null;
            }
            Destroy(bullet);
            Flash(Color.white, 1f, 1.2f);
            Black = 1f;
            logoFrom = 114.2f; logoTo = 117.6f; logoHit = 114.2f;
            Text("GORE SANDBOX", 114.25f, 117.6f, Style.Logo, 0.58f, 130);
            Text("OUT NOW  —  ANDROID + PC", 115.0f, 117.6f, Style.Small, 0.74f, 34, new Color(1f, 0.75f, 0.45f));
            yield return At(117.6f);
            endFrom = 117.6f;
            Embers(new Rect(0, UI.VH * 0.6f, UI.VW, UI.VH * 0.4f), 120, new Color(1f, 0.5f, 0.15f), 140f);
            yield return At(119.6f);
            StartCoroutine(FadeBlack(0, 1f, 0.3f));
            endFrom = -1f;
        }

        // ====================================================================== drawing (TrailerScreen)

        public void DrawOverlay()
        {
            float W = UI.VW, H = UI.VH, t = T;
            var full = new Rect(-10, -10, W + 20, H + 20);
            bool repaint = Event.current.type == EventType.Repaint;

            // Colour grade + police lights
            float pulse = Pulse;
            if (washA > 0f) UI.Fill(full, washCol.WithAlpha(Mathf.Clamp01(washA + washPulse * pulse)));
            if (policePulse && t < B(43) + 15.5f)
            {
                bool red = Mathf.Repeat(t, TrailerMusic.Beat * 2f) < TrailerMusic.Beat;
                UI.Fill(full, (red ? new Color(1f, 0.05f, 0.05f) : new Color(0.1f, 0.25f, 1f)).WithAlpha(0.1f + 0.12f * pulse));
            }

            // Vignette + grain
            UI.Tex(full, TextureFactory.Vignette, new Color(0, 0, 0, 0.75f));
            if (repaint)
            {
                float gx = Random.value, gy = Random.value;
                UI.TexUV(full, TextureFactory.Grain, new Rect(gx, gy, W / 256f, H / 256f), new Color(1, 1, 1, 0.07f));
            }

            // Chromatic edges + speed lines on hits
            if (chroma > 0.01f)
            {
                UI.Tex(new Rect(-40, -10, W * 0.35f, H + 20), TextureFactory.Glow, new Color(1f, 0.1f, 0.15f, 0.35f * chroma));
                UI.Tex(new Rect(W * 0.65f + 40, -10, W * 0.35f, H + 20), TextureFactory.Glow, new Color(0.1f, 0.9f, 1f, 0.35f * chroma));
            }
            if (lines > 0.01f)
            {
                var m0 = GUI.matrix;
                var cen = new Vector2(W / 2f, H / 2f);
                for (int i = 0; i < 28; i++)
                {
                    GUI.matrix = m0;
                    GUIUtility.RotateAroundPivot(i * (360f / 28f) + (i * 37 % 11), cen * UI.Scale);
                    float len = W * 0.5f;
                    UI.Tex(new Rect(cen.x + len * 0.45f, cen.y - 3, len * 0.7f, 6), TextureFactory.Streak, new Color(1, 1, 1, 0.45f * lines));
                }
                GUI.matrix = m0;
            }
            if (ring >= 0f)
            {
                float r = Mathf.Lerp(40f, W * 0.9f, ring);
                UI.Tex(new Rect(W / 2 - r, H / 2 - r, r * 2, r * 2), TextureFactory.Ring, ringCol.WithAlpha(0.8f * (1f - ring)));
            }

            // Wanted stars
            if (starsFrom >= 0f && t >= starsFrom && t < starsTo)
            {
                bool on = Mathf.Repeat(t, 0.4f) < 0.25f;
                for (int i = 0; i < 5; i++) UI.Tex(new Rect(W - 120 - (4 - i) * 74, 130, 64, 64), IconFactory.Get("star"), on ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.25f, 0.2f));
            }

            DrawFeature(W, H, t);

            // Black / logo / end card
            if (Black > 0.001f) UI.Fill(full, new Color(0, 0, 0, Black));
            if (logoFrom >= 0f && t >= logoFrom && t < logoTo) DrawEclipse(W, H, t);
            if (endFrom >= 0f && t >= endFrom) DrawEndCard(W, H, t);

            // Text cards
            foreach (var c in cards) if (t >= c.From && t < c.To) DrawCard(c, W, H, t);

            // Embers
            foreach (var e in embers)
            {
                float a = Mathf.Clamp01(e.life / e.max);
                UI.Tex(new Rect(e.pos.x - e.size, e.pos.y - e.size, e.size * 2, e.size * 2), TextureFactory.Glow, e.col.WithAlpha(a));
            }

            // Flashes, freeze frames, impact frames
            if (flashA > 0.001f) UI.Fill(full, flashCol.WithAlpha(flashA));
            if (t >= 56.1f && t < whiteUntil) UI.Fill(full, Color.white);
            else if (t >= whiteUntil && t < blackUntil) UI.Fill(full, Color.black);
            if (impactFrames > 0)
            {
                UI.Fill(full, impactFrames % 2 == 1 ? impactCol : new Color(0, 0, 0, 0.85f));
                if (repaint) impactFrames--;
            }

            // Cinematic bars
            float bar = H * 0.105f * Letterbox;
            UI.Fill(new Rect(-10, -10, W + 20, bar + 10), Color.black);
            UI.Fill(new Rect(-10, H - bar, W + 20, bar + 10), Color.black);
        }

        void DrawEclipse(float W, float H, float t)
        {
            float u = Mathf.Clamp01((t - logoFrom) / Mathf.Max(0.01f, logoHit - logoFrom));
            float after = Mathf.Max(0f, t - logoHit);
            float fadeOut = Mathf.Clamp01((logoTo - t) / 0.6f);
            var c = new Vector2(W / 2f, H * 0.4f);
            float r = Mathf.Lerp(20f, 150f, E(u)) + (after > 0 ? 8f * Mathf.Exp(-after * 3f) : 0f);
            float corona = (0.25f + 0.75f * u * u + (after > 0 ? 0.6f * Mathf.Exp(-after * 2f) : 0f) + 0.25f * Pulse) * fadeOut;
            UI.Tex(new Rect(c.x - r * 4.2f, c.y - r * 4.2f, r * 8.4f, r * 8.4f), TextureFactory.Glow, new Color(0.55f, 0.15f, 0.9f, 0.45f * corona));
            UI.Tex(new Rect(c.x - r * 2.6f, c.y - r * 2.6f, r * 5.2f, r * 5.2f), TextureFactory.Corona, new Color(1f, 0.55f, 0.15f, 0.9f * corona));
            UI.Tex(new Rect(c.x - r * 1.4f, c.y - r * 1.4f, r * 2.8f, r * 2.8f), TextureFactory.Glow, new Color(1f, 0.8f, 0.5f, 0.6f * corona));
            UI.Tex(new Rect(c.x - r, c.y - r, r * 2, r * 2), TextureFactory.Circle, new Color(0, 0, 0, fadeOut));
            UI.Tex(new Rect(c.x - r * 1.04f, c.y - r * 1.04f, r * 2.08f, r * 2.08f), TextureFactory.Ring, new Color(1f, 0.85f, 0.6f, 0.6f * corona));
        }

        void DrawEndCard(float W, float H, float t)
        {
            float a = Mathf.Clamp01((t - endFrom) / 0.4f) * Mathf.Clamp01((119.7f - t) / 0.3f);
            var c = new Vector2(W / 2f, H * 0.42f);
            UI.Tex(new Rect(c.x - 70, c.y - 190, 140, 140), TextureFactory.Corona, new Color(1f, 0.55f, 0.15f, 0.8f * a));
            UI.Tex(new Rect(c.x - 34, c.y - 154, 68, 68), TextureFactory.Circle, new Color(0, 0, 0, a));
            Chroma(new Rect(0, c.y - 40, W, 90), "SolarEclipse Productions", 72, Color.white.WithAlpha(a), UI.HeavyFont, 3f * a);
            var br = new Rect(c.x - 190, c.y + 90, 380, 92);
            float pop = 1f + 0.06f * Pulse + 0.04f * Mathf.Sin(t * 6f);
            var m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), br.center * UI.Scale);
            UI.Rounded(br, new Color(0.85f, 0.05f, 0.05f, a));
            UI.Tex(new Rect(br.x + 26, br.y + 22, 48, 48), IconFactory.Get("bell"), Color.white.WithAlpha(a));
            UI.Label(new Rect(br.x + 70, br.y, br.width - 80, br.height), "SUBSCRIBE", 40, Color.white.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Bold, false, false, UI.HeavyFont);
            GUI.matrix = m;
        }

        void DrawFeature(float W, float H, float t)
        {
            if (feature < 0) return;
            if (feature == 0)
            {
                // A story dialogue box (like the game's cutscenes)
                var r = new Rect(W * 0.18f, H * 0.66f, W * 0.64f, 150);
                UI.Rounded(r, new Color(0.04f, 0.03f, 0.06f, 0.88f));
                UI.Fill(new Rect(r.x, r.y, 6, r.height), new Color(0.7f, 0.35f, 1f));
                UI.Label(new Rect(r.x + 26, r.y + 10, r.width - 40, 40), "BIG MARCUS", 26, new Color(0.8f, 0.55f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
                string line = "You want in? Then don't miss.";
                int n = Mathf.Clamp(Mathf.FloorToInt((t - featureFrom - 0.2f) * 30f), 0, line.Length);
                UI.Label(new Rect(r.x + 26, r.y + 52, r.width - 40, 80), line.Substring(0, n), 30, Color.white, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            }
            else if (feature == 3)
            {
                // Phone + PC
                var pc = new Rect(W * 0.18f, H * 0.3f, W * 0.42f, H * 0.42f);
                UI.Rounded(new Rect(pc.x - 14, pc.y - 14, pc.width + 28, pc.height + 28), new Color(0.06f, 0.06f, 0.07f, 0.95f));
                UI.Gradient(pc, new Color(0.3f, 0.05f, 0.08f), new Color(0.02f, 0.02f, 0.03f));
                UI.Label(pc, GameInfo.Title, 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
                UI.Fill(new Rect(pc.center.x - 40, pc.yMax + 14, 80, 50), new Color(0.06f, 0.06f, 0.07f, 0.95f));
                UI.Fill(new Rect(pc.center.x - 130, pc.yMax + 62, 260, 14), new Color(0.06f, 0.06f, 0.07f, 0.95f));
                var ph = new Rect(W * 0.66f, H * 0.24f, 230, 440);
                UI.Rounded(new Rect(ph.x - 12, ph.y - 12, ph.width + 24, ph.height + 24), new Color(0.06f, 0.06f, 0.07f, 0.97f));
                UI.Tex(ph, OWHud.MapTex, Color.white, ScaleMode.ScaleAndCrop);
                UI.Tex(new Rect(ph.center.x - 18, ph.center.y - 18, 36, 36), IconFactory.Get("pin_map"), new Color(1f, 0.3f, 0.9f));
            }
        }

        void DrawCard(Card c, float W, float H, float t)
        {
            float age = t - c.From, left = c.To - t;
            float a = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01(left / 0.2f);
            int size = c.Size > 0 ? c.Size : 60;
            float y = H * c.Y;
            switch (c.Style)
            {
                case Style.Thin:
                {
                    int n = Mathf.Clamp(Mathf.FloorToInt(age * 22f), 0, c.Text.Length);
                    float fa = Mathf.Clamp01(left / 0.6f);
                    Chroma(new Rect(0, y - size, W, size * 2), c.Text.Substring(0, n), size, c.Color.WithAlpha(fa), UI.Font, 1.5f);
                    if (left < 0.6f && !c.Embered) { c.Embered = true; Embers(new Rect(W * 0.25f, y - 30, W * 0.5f, 60), 120, new Color(1f, 0.45f, 0.15f), 90f); }
                    break;
                }
                case Style.Slide:
                {
                    float x = Mathf.Lerp(-500f, 0f, E(Mathf.Clamp01(age / 0.45f)));
                    UI.Fill(new Rect(W * 0.08f + x, y - size * 0.6f, 8, size * 1.2f), UI.Accent.WithAlpha(a));
                    UI.Label(new Rect(W * 0.08f + 30 + x, y - size, W, size * 2), c.Text, size, c.Color.WithAlpha(a), TextAnchor.MiddleLeft, FontStyle.Bold, true, false, UI.HeavyFont);
                    break;
                }
                case Style.Slam:
                {
                    float s = Mathf.Lerp(1.7f, 1f, E(Mathf.Clamp01(age / 0.1f)));
                    float jitter = Mathf.Exp(-age * 6f) * 10f;
                    var off = new Vector2(Mathf.Sin(t * 97f) * jitter, Mathf.Cos(t * 71f) * jitter);
                    var m = GUI.matrix;
                    GUIUtility.ScaleAroundPivot(new Vector2(s, s), new Vector2(W / 2f, y) * UI.Scale);
                    Chroma(new Rect(off.x, y - size + off.y, W, size * 2), c.Text, size, c.Color.WithAlpha(a), UI.HeavyFont, 4f + 14f * Mathf.Exp(-age * 5f));
                    GUI.matrix = m;
                    if (age < 0.05f && !c.Embered) { c.Embered = true; Flash(Color.white, 0.25f, 6f); Shake(0.35f); }
                    break;
                }
                case Style.Small:
                    UI.Label(new Rect(0, y - size, W, size * 2), c.Text, size, c.Color.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Bold, true);
                    break;
                case Style.Logo:
                {
                    float s = Mathf.Lerp(1.5f, 1f, E(Mathf.Clamp01(age / 0.12f)));
                    float fade = Mathf.Clamp01(left / 0.6f);
                    float glitch = Mathf.Repeat(t * 7.3f, 1f) < 0.06f ? 18f : 0f;
                    var m = GUI.matrix;
                    GUIUtility.ScaleAroundPivot(new Vector2(s, s), new Vector2(W / 2f, y) * UI.Scale);
                    Chroma(new Rect(glitch, y - size, W, size * 2), c.Text, size, new Color(0.97f, 0.95f, 0.9f, fade), UI.HeavyFont, 3f + 10f * Mathf.Exp(-age * 3f));
                    GUI.matrix = m;
                    UI.Fill(new Rect(W / 2 - 360 * E(Mathf.Clamp01(age / 0.5f)), y + size * 0.62f, 720 * E(Mathf.Clamp01(age / 0.5f)), 4), UI.Accent.WithAlpha(fade));
                    break;
                }
                case Style.Card:
                {
                    var r = new Rect(0, H * c.Y - 50, W, 100);
                    UI.Fill(new Rect(0, r.y + 10, W, 80), new Color(0, 0, 0, 0.45f * a));
                    Chroma(r, c.Text, size, c.Color.WithAlpha(a), UI.HeavyFont, 3f);
                    break;
                }
            }
        }

        /// <summary>Text with a red / cyan split edge.</summary>
        static void Chroma(Rect r, string text, int size, Color col, Font font, float split)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (split > 0.2f)
            {
                UI.Label(new Rect(r.x - split, r.y, r.width, r.height), text, size, new Color(1f, 0.1f, 0.15f, col.a * 0.7f), TextAnchor.MiddleCenter, FontStyle.Normal, false, false, font);
                UI.Label(new Rect(r.x + split, r.y, r.width, r.height), text, size, new Color(0.1f, 0.9f, 1f, col.a * 0.7f), TextAnchor.MiddleCenter, FontStyle.Normal, false, false, font);
            }
            UI.Label(r, text, size, col, TextAnchor.MiddleCenter, FontStyle.Normal, true, false, font);
        }
    }

    /// <summary>Moves a tracer / rocket in a straight line on scaled time, then removes it.</summary>
    public sealed class TrailerMover : MonoBehaviour
    {
        public Vector3 Velocity; public float Life = 1f;
        void Update()
        {
            transform.position += Velocity * Time.deltaTime;
            Life -= Time.deltaTime;
            if (Life <= 0f) Destroy(gameObject);
        }
    }

    /// <summary>The trailer's overlay: everything on top of the 3D picture. Esc stops the trailer.</summary>
    public sealed class TrailerScreen : UIScreen
    {
        readonly TrailerDirector d;
        public TrailerScreen(TrailerDirector director) { d = director; }
        public override bool BlocksGameplay => true;
        public override bool ShowCursor => false;
        public override void Draw() { if (d != null) d.DrawOverlay(); }
        public override bool OnBack() { if (d != null) d.Stop(); return true; }
    }

    /// <summary>"Preparing the trailer" while the music is generated, then the studio map loads.</summary>
    public sealed class TrailerPrepScreen : UIScreen
    {
        public override bool IsModal => true;
        bool started;
        public override void Tick()
        {
            if (!started && TrailerDirector.TickPrep()) started = true;
        }
        public override bool OnBack() { if (!started) { UIRoot.Remove(this); } return true; }
        public override void Draw()
        {
            UI.Fill(new Rect(-10, -10, UI.VW + 20, UI.VH + 20), Color.black);
            float p = TrailerDirector.PrepProgress;
            UI.Label(new Rect(0, UI.VH * 0.42f, UI.VW, 60), "SOLARECLIPSE PRODUCTIONS", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true, false, UI.HeavyFont);
            UI.Label(new Rect(0, UI.VH * 0.5f, UI.VW, 40), "Preparing the trailer... " + Mathf.RoundToInt(p * 100f) + "%   (screen-record it: the HUD stays hidden; Esc stops it)", 20, UI.Muted, TextAnchor.MiddleCenter);
            UI.Fill(new Rect(UI.VW / 2 - 300, UI.VH * 0.57f, 600, 6), new Color(1, 1, 1, 0.1f));
            UI.Fill(new Rect(UI.VW / 2 - 300, UI.VH * 0.57f, 600 * p, 6), UI.Accent);
        }
    }
}
