using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SolarEclipse.Audio;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 intro, the 3D half (UI/Screens/Intro2Screen.cs owns the clock and draws titles and fades).
    /// "The Morgue": a heartbeat in the dark; a slow dolly down a tiled morgue under flickering tubes to a body under a
    /// blood-stained sheet; close on the hand hanging off the gurney, dripping into a pool; the fingers twitch, the body
    /// jolts and gasps, every light bursts, the monitor flatlines, and the title cuts in.
    /// Built far from the menu backdrop; cameras, listeners, fog, ambient light and the sun are restored on Dispose.
    /// Phone budget: ~40 boxes, 4 lights without shadows, no physics bodies.
    /// </summary>
    public sealed class Intro2Cinematic : MonoBehaviour
    {
        public const float RoomTime = RecipesIntro2.RoomTime, CloseTime = RecipesIntro2.CloseTime, TwitchTime = RecipesIntro2.TwitchTime,
            JoltTime = RecipesIntro2.JoltTime, BurstTime = RecipesIntro2.BurstTime, FlatTime = RecipesIntro2.FlatTime,
            TitleTime = RecipesIntro2.TitleTime, SwapTime = RecipesIntro2.SwapTime, EndTime = RecipesIntro2.EndTime;

        static readonly Vector3 Origin = new Vector3(0f, 0f, 800f);
        static Vector3 L(float x, float y, float z) => Origin + new Vector3(x, y, z);
        static readonly Vector3 Gurney = L(0f, 0f, 2.5f);
        static readonly Color FogColor = new Color(0.02f, 0.03f, 0.03f);

        Transform set;
        Camera cam;
        readonly List<Light> tubes = new List<Light>();
        readonly List<Renderer> tubeGlow = new List<Renderer>();
        Light monitorLight;
        StaticHuman body;
        Transform sheet;
        LineRenderer ecg;
        Material ecgGreen, ecgRed;
        AudioSource music, sfx;
        float nextDrip, ecgX;
        bool burst;
        readonly float[] ecgBuf = new float[64];
        Vector3 sheetBase;

        public float Shake { get; private set; }
        public float Flash { get; private set; }

        readonly List<Behaviour> disabled = new List<Behaviour>();
        bool savedFog, sunWasOn, disposed;
        FogMode savedFogMode;
        Color savedFogColor, savedAmb;
        float savedFogDensity;
        AmbientMode savedAmbMode;

        public static Intro2Cinematic Create()
        {
            var c = new GameObject("Intro2Cinematic").AddComponent<Intro2Cinematic>();
            c.Build();
            return c;
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            foreach (var c in Camera.allCameras) if (c != null && c.enabled) { c.enabled = false; disabled.Add(c); }
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l != null && l.enabled) { l.enabled = false; disabled.Add(l); }
            sunWasOn = MapLighting.Sun != null && MapLighting.Sun.enabled;
            if (sunWasOn) MapLighting.Sun.enabled = false;
            savedFog = RenderSettings.fog; savedFogMode = RenderSettings.fogMode; savedFogColor = RenderSettings.fogColor; savedFogDensity = RenderSettings.fogDensity;
            savedAmbMode = RenderSettings.ambientMode; savedAmb = RenderSettings.ambientLight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.055f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.065f, 0.065f) * Settings.Data.brightness;

            set = new GameObject("MorgueSet").transform;
            set.SetParent(transform, false);
            var b = new MapBuilder(set, null, new MapOptions());
            var tiles = MapBuilder.Tex(TextureFactory.Tiles);
            var metal = MapBuilder.Tex(TextureFactory.Metal);
            var tileC = new Color(0.55f, 0.62f, 0.58f);
            b.Block("Floor", L(-4f, -0.3f, -9f), L(4f, 0f, 8f), tiles, SurfaceType.Concrete, new Color(0.42f, 0.46f, 0.44f));
            b.Block("Ceiling", L(-4f, 3.4f, -9f), L(4f, 3.7f, 8f), tiles, SurfaceType.Concrete, new Color(0.3f, 0.32f, 0.31f));
            b.Block("Wall L", L(-4.3f, 0f, -9f), L(-4f, 3.4f, 8f), tiles, SurfaceType.Concrete, tileC);
            b.Block("Wall R", L(4f, 0f, -9f), L(4.3f, 3.4f, 8f), tiles, SurfaceType.Concrete, tileC);
            b.Block("Back Wall", L(-4f, 0f, 8f), L(4f, 3.4f, 8.3f), tiles, SurfaceType.Concrete, tileC);
            b.Block("Front Wall", L(-4f, 0f, -9.3f), L(4f, 3.4f, -9f), tiles, SurfaceType.Concrete, tileC);
            // Wall of cold-storage drawers on the left.
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 6; c++)
                {
                    var p = L(-3.92f, 0.55f + r * 0.95f, -6f + c * 1.3f);
                    b.Box("Drawer", p, Quaternion.identity, new Vector3(0.14f, 0.82f, 1.18f), metal, SurfaceType.Metal, new Color(0.62f, 0.64f, 0.66f));
                    b.Box("Handle", p + new Vector3(0.1f, 0.18f, 0f), Quaternion.identity, new Vector3(0.05f, 0.04f, 0.4f), Mats.LitMetal, SurfaceType.Metal, new Color(0.8f, 0.8f, 0.82f), 1f, false);
                }
            // Double doors at the far end, a drain, a sink and a steel tray of instruments.
            b.Box("Door", L(-0.62f, 1.1f, 7.95f), Quaternion.identity, new Vector3(1.2f, 2.2f, 0.08f), metal, SurfaceType.Metal, new Color(0.3f, 0.34f, 0.33f));
            b.Box("Door", L(0.62f, 1.1f, 7.95f), Quaternion.identity, new Vector3(1.2f, 2.2f, 0.08f), metal, SurfaceType.Metal, new Color(0.3f, 0.34f, 0.33f));
            b.Box("Window", L(0.62f, 1.6f, 7.9f), Quaternion.identity, new Vector3(0.35f, 0.45f, 0.02f), Mats.Glow(new Color(0.25f, 0.35f, 0.45f), 0.6f), SurfaceType.Glass, null, 1f, false);
            b.Cylinder("Drain", L(0.4f, 0.005f, 1.4f), 0.18f, 0.01f, Mats.LitMetal, SurfaceType.Metal, new Color(0.15f, 0.15f, 0.16f), 16);
            b.Box("Sink", L(3.7f, 0.9f, -2f), Quaternion.identity, new Vector3(0.55f, 0.25f, 1.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.75f, 0.77f, 0.8f));
            b.Box("Tray", L(1.3f, 0.95f, 3.4f), Quaternion.identity, new Vector3(0.5f, 0.03f, 0.35f), Mats.LitMetal, SurfaceType.Metal, new Color(0.78f, 0.8f, 0.82f));
            b.Box("Tray Leg", L(1.3f, 0.47f, 3.4f), Quaternion.identity, new Vector3(0.04f, 0.94f, 0.04f), Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.6f, 0.62f), 1f, false);
            for (int k = 0; k < 5; k++)
                b.Box("Instrument", L(1.14f + k * 0.08f, 0.975f, 3.4f), Quaternion.Euler(0, 8 * k - 16, 0), new Vector3(0.012f, 0.008f, 0.24f), Mats.LitMetal, SurfaceType.Metal, new Color(0.88f, 0.89f, 0.92f), 1f, false);

            // The gurney.
            b.Box("Gurney Top", Gurney + new Vector3(0, 0.88f, 0), Quaternion.identity, new Vector3(0.72f, 0.06f, 2.05f), metal, SurfaceType.Metal, new Color(0.72f, 0.74f, 0.76f));
            foreach (var sx in new[] { -0.3f, 0.3f })
                foreach (var sz in new[] { -0.9f, 0.9f })
                {
                    b.Box("Gurney Leg", Gurney + new Vector3(sx, 0.45f, sz), Quaternion.identity, new Vector3(0.04f, 0.82f, 0.04f), Mats.LitMetal, SurfaceType.Metal, new Color(0.55f, 0.56f, 0.58f), 1f, false);
                    b.Cylinder("Wheel", Gurney + new Vector3(sx, 0.0f, sz), 0.06f, 0.05f, Mats.LitMetal, SurfaceType.Metal, new Color(0.1f, 0.1f, 0.1f), 10);
                }
            // Heart monitor on a stand by the head end.
            b.Box("Monitor Stand", Gurney + new Vector3(0.75f, 0.65f, 1.15f), Quaternion.identity, new Vector3(0.05f, 1.3f, 0.05f), Mats.LitMetal, SurfaceType.Metal, new Color(0.4f, 0.4f, 0.42f), 1f, false);
            b.Box("Monitor", Gurney + new Vector3(0.75f, 1.45f, 1.15f), Quaternion.Euler(0, -120, 0), new Vector3(0.42f, 0.3f, 0.12f), Mats.Lit, SurfaceType.Metal, new Color(0.08f, 0.08f, 0.09f));
            b.Finish();

            // Fluorescent tubes (lights we flicker and burst).
            for (int i = 0; i < 3; i++)
            {
                var p = L(0f, 3.32f, -5f + i * 4f);
                var glow = MakeRenderer("Tube", Cube(), Mats.Glow(new Color(0.85f, 1f, 0.95f), 2.4f), p, new Vector3(0.14f, 0.05f, 1.6f));
                tubeGlow.Add(glow);
                var lg = new GameObject("TubeLight").AddComponent<Light>();
                lg.transform.SetParent(set, false);
                lg.transform.position = p + Vector3.down * 0.2f;
                lg.type = LightType.Point; lg.range = 7.5f; lg.intensity = 1.5f; lg.color = new Color(0.8f, 1f, 0.92f); lg.shadows = LightShadows.None;
                tubes.Add(lg);
            }
            var ml = new GameObject("MonitorLight").AddComponent<Light>();
            ml.transform.SetParent(set, false);
            ml.transform.position = Gurney + new Vector3(0.62f, 1.45f, 1.0f);
            ml.type = LightType.Point; ml.range = 1.6f; ml.intensity = 1.2f; ml.color = new Color(0.3f, 1f, 0.45f); ml.shadows = LightShadows.None;
            monitorLight = ml;

            // ECG trace across the monitor face.
            var screen = Gurney + new Vector3(0.75f, 1.45f, 1.15f);
            var ecgGo = new GameObject("ECG");
            ecgGo.transform.SetParent(set, false);
            ecg = ecgGo.AddComponent<LineRenderer>();
            ecg.positionCount = ecgBuf.Length;
            ecg.widthMultiplier = 0.008f;
            ecg.useWorldSpace = true;
            ecg.shadowCastingMode = ShadowCastingMode.Off; ecg.receiveShadows = false;
            ecgGreen = Mats.Glow(new Color(0.25f, 1f, 0.4f), 3f);
            ecgRed = Mats.Glow(new Color(1f, 0.15f, 0.1f), 3f);
            ecg.sharedMaterial = ecgGreen;
            ecgScreen = screen + Quaternion.Euler(0, -120, 0) * Vector3.back * 0.065f;
            ecgRight = Quaternion.Euler(0, -120, 0) * Vector3.left;

            // The body on the gurney: face up, head toward the doors, right arm hanging over the side.
            var look = Appearance.Random(-1);
            body = StaticHuman.Create(set, look, Layers.Effects, "TheBody");
            body.transform.SetPositionAndRotation(Gurney + new Vector3(0f, 1.03f, -0.9f), Quaternion.LookRotation(Vector3.up, Vector3.forward));
            body.Set(PartType.UpperArmR, 80f, 0f, 25f);
            body.Set(PartType.ForearmR, -8f);
            body.Set(PartType.HandR, 10f);
            body.Set(PartType.UpperArmL, 0f, 0f, -6f);
            if (body.Face != null) body.Face.Expression = Expression.Dead;

            // The sheet, stained over the chest.
            sheet = MakeSheet().transform;
            sheetBase = sheet.localPosition;

            // Blood under the hand, already pooled.
            var hand = body.PartPosition(PartType.HandR, new Vector3(0, -0.12f, 0));
            Decals.Blood(new Vector3(hand.x, Origin.y + 0.002f, hand.z), Vector3.up, 0.75f, 2, null);
            for (int i = 0; i < 6; i++)
                Decals.Blood(Gurney + new Vector3(Random.Range(-1.6f, 1.6f), 0.002f, Random.Range(-2.5f, 2.5f)), Vector3.up, Random.Range(0.15f, 0.5f), 3, null);

            var camGo = new GameObject("Intro2Camera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 40f;
            cam.depth = 10f;
            cam.cullingMask = ~((1 << Layers.Viewmodel) | (1 << 31));
            camGo.AddComponent<AudioListener>();

            music = gameObject.AddComponent<AudioSource>(); music.playOnAwake = false; music.spatialBlend = 0f;
            sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f;
            var score = SoundBank.Get("intro2_score");
            if (score != null) { music.clip = score; music.volume = Settings.Data.musicVolume; music.Play(); }
            Step(0f, -0.001f, 0f);
        }

        Vector3 ecgScreen, ecgRight;

        static Mesh cube;
        static Mesh Cube()
        {
            if (cube != null) return cube;
            var mb = new MeshBuilder { Color = Color.white, Gloss = 0f };
            mb.Box(Vector3.zero, Vector3.one);
            return cube = mb.ToMesh("Intro2Cube");
        }

        Renderer MakeRenderer(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(set, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return mr;
        }

        /// <summary>A sheet draped over the body: a grid that follows a rough body profile and falls over the sides.</summary>
        GameObject MakeSheet()
        {
            const int nx = 12, nz = 34;
            float halfW = 0.46f, len = 2.05f;
            var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = x / (float)nx, v = z / (float)nz;
                    float px = (u - 0.5f) * 2f * halfW, pz = (v - 0.5f) * len;
                    // Height of the body under the sheet along its length (feet -> head) and across it.
                    float along = v;
                    float prof = along < 0.08f ? 0.12f : along < 0.45f ? 0.14f : along < 0.62f ? 0.17f : along < 0.82f ? 0.22f : along < 0.9f ? 0.12f : 0.2f;
                    float across = Mathf.Clamp01(1f - Mathf.Pow(Mathf.Abs(px) / 0.24f, 2f));
                    float y = 0.035f + prof * across;
                    // Past the gurney edge the cloth hangs down.
                    float over = Mathf.Abs(px) - 0.36f;
                    if (over > 0f) y -= over * 2.2f;
                    y += Mathf.Sin(px * 30f + pz * 7f) * 0.004f; // folds
                    verts.Add(new Vector3(px, y, pz));
                    // Blood soaked through over the chest and near the hanging arm.
                    float stain = Mathf.Clamp01(1f - new Vector2((px + 0.1f) / 0.18f, (along - 0.7f) / 0.08f).magnitude) + Mathf.Clamp01(1f - new Vector2((px + 0.38f) / 0.12f, (along - 0.62f) / 0.1f).magnitude) * 0.8f;
                    var c = Color.Lerp(new Color(0.86f, 0.86f, 0.84f), new Color(0.35f, 0.03f, 0.03f), Mathf.Clamp01(stain));
                    c.a = 0.15f;
                    cols.Add(c);
                }
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int i = z * (nx + 1) + x;
                    tris.Add(i); tris.Add(i + nx + 1); tris.Add(i + 1);
                    tris.Add(i + 1); tris.Add(i + nx + 1); tris.Add(i + nx + 2);
                    // back faces so the hanging edge is visible from below
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + nx + 1);
                    tris.Add(i + 1); tris.Add(i + nx + 2); tris.Add(i + nx + 1);
                }
            var m = new Mesh { name = "Sheet" };
            m.SetVertices(verts); m.SetColors(cols); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            var go = new GameObject("Sheet");
            go.transform.SetParent(set, false);
            go.transform.position = Gurney + new Vector3(0f, 0.92f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            return go;
        }

        // ------------------------------------------------------------------ timeline

        static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>Advances the cinematic to time t (seconds). Events between prevT and t fire once.</summary>
        public void Step(float t, float prevT, float dt)
        {
            if (disposed) return;
            bool At(float m) => prevT < m && t >= m;
            Shake *= Mathf.Exp(-dt * 3f);
            Flash *= Mathf.Exp(-dt * 6f);
            if (music != null) music.volume = Settings.Data.musicVolume;

            // Lights: flicker, then burst.
            bool flick = RecipesIntro2.Flicker(t);
            if (At(BurstTime) && !burst)
            {
                burst = true;
                Flash = 0.8f; Shake = 1f;
                foreach (var l in tubes) if (l != null) FX.Sparks(l.transform.position, Vector3.down, 18);
            }
            for (int i = 0; i < tubes.Count; i++)
            {
                bool off = burst || (flick && (i == 1 || t > 7f));
                float k = 0.95f + 0.05f * Mathf.PerlinNoise(t * 9f, i);
                if (tubes[i] != null) tubes[i].intensity = off ? 0f : 1.5f * k * (t > JoltTime && t < BurstTime ? 1.6f : 1f);
                if (tubeGlow[i] != null) tubeGlow[i].enabled = !off;
            }

            // ECG: beats with the score's heart rate; flatline (red) after the burst.
            float bpm = RecipesIntro2.Bpm(t);
            ecgX += dt * 1.6f;
            for (int i = 0; i < ecgBuf.Length - 1; i++) ecgBuf[i] = ecgBuf[i + 1];
            float phase = (t * bpm / 60f) % 1f;
            ecgBuf[ecgBuf.Length - 1] = t >= FlatTime ? 0f : Pqrst(phase);
            if (ecg != null)
            {
                if (At(FlatTime)) { ecg.sharedMaterial = ecgRed; if (monitorLight != null) monitorLight.color = new Color(1f, 0.2f, 0.15f); }
                for (int i = 0; i < ecgBuf.Length; i++)
                    ecg.SetPosition(i, ecgScreen + ecgRight * (i / (float)(ecgBuf.Length - 1) - 0.5f) * 0.36f + Vector3.up * ecgBuf[i] * 0.09f);
            }

            // The hand drips into the pool; fingers twitch; the body jolts.
            if (body != null)
            {
                if (t > CloseTime - 1f && t < BurstTime && Time.time >= nextDrip)
                {
                    nextDrip = Time.time + 0.55f;
                    BloodFX.Drip(body.PartPosition(PartType.HandR, new Vector3(0, -0.16f, 0.01f)), Vector3.zero);
                }
                float tw = t >= TwitchTime && t < TwitchTime + 0.6f ? Mathf.Sin((t - TwitchTime) * 40f) * (1f - (t - TwitchTime) / 0.6f) : 0f;
                body.Set(PartType.HandR, 10f + tw * 35f, 0f, tw * 10f);
                float jolt = t >= JoltTime ? Mathf.Exp(-(t - JoltTime) * 6f) * Mathf.Clamp01((t - JoltTime) / 0.05f) : 0f;
                body.Set(PartType.Chest, -jolt * 18f);
                body.Set(PartType.Head, -jolt * 30f);
                body.Set(PartType.ForearmR, -8f - jolt * 40f);
                if (sheet != null) sheet.localPosition = sheetBase + Vector3.up * jolt * 0.06f;
                if (At(JoltTime))
                {
                    Shake = 0.6f;
                    if (body.Face != null) body.Face.Expression = Expression.Afraid;
                    var gasp = SoundBank.Get("v_gasp");
                    if (gasp != null) sfx.PlayOneShot(gasp, Settings.Data.voiceVolume);
                }
                if (At(FlatTime) && body.Face != null) body.Face.Expression = Expression.Dead;
            }

            // Camera: a slow dolly down the room, then close on the hand, pushing in and tilting up to the sheet.
            if (cam != null)
            {
                Vector3 pos; Vector3 at;
                if (t < CloseTime)
                {
                    float k = S((t - RoomTime + 0.5f) / (CloseTime - RoomTime + 0.5f));
                    pos = Vector3.Lerp(L(-1.1f, 1.6f, -7.5f), L(-0.75f, 1.35f, -1.6f), k);
                    at = Vector3.Lerp(Gurney + new Vector3(0f, 0.6f, 1.5f), Gurney + new Vector3(-0.1f, 0.9f, 0.2f), k);
                }
                else
                {
                    float k = S((t - CloseTime) / (BurstTime - CloseTime));
                    var hand = body != null ? body.PartPosition(PartType.HandR) : Gurney;
                    pos = Vector3.Lerp(Gurney + new Vector3(-1.55f, 0.72f, -0.55f), Gurney + new Vector3(-1.05f, 0.95f, -0.15f), k);
                    at = Vector3.Lerp(hand + Vector3.down * 0.1f, Gurney + new Vector3(-0.15f, 1.05f, 0.45f), S((t - TwitchTime) / (JoltTime - TwitchTime)));
                }
                float sway = 0.012f;
                pos += new Vector3(Mathf.PerlinNoise(t * 0.5f, 1f) - 0.5f, Mathf.PerlinNoise(2f, t * 0.5f) - 0.5f, 0f) * sway * 2f;
                pos += Random.insideUnitSphere * Shake * 0.03f;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(at - pos));
                if (burst) cam.cullingMask = 0; // pitch black after the lights burst
            }
        }

        /// <summary>One heartbeat on an ECG: small P wave, sharp QRS spike, rounded T wave.</summary>
        static float Pqrst(float p)
        {
            float v = 0f;
            v += 0.15f * Mathf.Exp(-Mathf.Pow((p - 0.15f) / 0.035f, 2f));
            v -= 0.18f * Mathf.Exp(-Mathf.Pow((p - 0.27f) / 0.008f, 2f));
            v += 1.0f * Mathf.Exp(-Mathf.Pow((p - 0.3f) / 0.01f, 2f));
            v -= 0.25f * Mathf.Exp(-Mathf.Pow((p - 0.33f) / 0.01f, 2f));
            v += 0.3f * Mathf.Exp(-Mathf.Pow((p - 0.55f) / 0.06f, 2f));
            return v;
        }

        public void StopAudio()
        {
            if (music != null) music.Stop();
            if (sfx != null) sfx.Stop();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            StopAudio();
            Restore();
            if (cam != null) { cam.gameObject.SetActive(false); Destroy(cam.gameObject); cam = null; }
            Destroy(gameObject);
        }

        void Restore()
        {
            RenderSettings.fog = savedFog; RenderSettings.fogMode = savedFogMode; RenderSettings.fogColor = savedFogColor; RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.ambientMode = savedAmbMode; RenderSettings.ambientLight = savedAmb;
            if (sunWasOn && MapLighting.Sun != null) MapLighting.Sun.enabled = true;
            foreach (var b in disabled) if (b != null) b.enabled = true;
            disabled.Clear();
        }

        void OnDestroy()
        {
            if (!disposed) { disposed = true; Restore(); }
        }
    }
}
