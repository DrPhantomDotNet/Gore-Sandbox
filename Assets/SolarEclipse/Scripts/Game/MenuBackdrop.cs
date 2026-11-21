using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The live 3D scene behind the main menu: a blood-moon stage with an idle Goredoll that watches the
    /// camera. Click it (poke) and it reacts; poke it enough and it falls over.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        static MenuBackdrop current;
        Camera cam;
        Goredoll doll;
        float orbit, nextIdle, pokeCooldown;
        // Gore Sandbox 2 operating room (Settings.Data.menuScene == 0)
        bool operatingRoom;
        Vector3 focus = new Vector3(0.6f, 1.05f, 1.2f);
        float baseYaw = 200f, dist = 4.2f;
        LineRenderer ecg;
        readonly float[] ecgBuf = new float[60];
        Vector3 ecgAt, ecgRight;
        StaticHuman patient;
        Light lampLight;
        float nextDrip;
        int pokes;
        static readonly string[] PokeLines = { "v_hey", "v_ow", "v_huh", "v_no", "v_whoa" };

        /// <summary>The live menu camera (the intro cinematic flies into its pose), or null.</summary>
        public static Camera Cam => current != null ? current.cam : null;

        public static void Create()
        {
            if (current != null) return;
            var go = new GameObject("MenuBackdrop");
            current = go.AddComponent<MenuBackdrop>();
            current.Build();
        }

        public static void DestroyBackdrop()
        {
            if (current == null) return;
            if (current.doll != null) current.doll.Despawn();
            Destroy(current.gameObject);
            current = null;
        }

        void Build()
        {
            if (Settings.Data.menuScene == 0) { BuildOperatingRoom(); return; }
            if (Settings.Data.menuScene == 2) { BuildSlaughterhouse(); return; }
            if (Settings.Data.menuScene == 3) { BuildAlley(); return; }
            var root = transform;
            MapLighting.Apply(TimeOfDay.BloodMoon, false, root);
            var b = new MapBuilder(root, null, new MapOptions());
            var floor = MapBuilder.Tex(TextureFactory.Concrete);
            b.Block("Stage", new Vector3(-12, -0.5f, -12), new Vector3(12, 0, 12), floor, SurfaceType.Concrete, new Color(0.45f, 0.38f, 0.38f));
            b.Block("BackWall", new Vector3(-12, 0, 6), new Vector3(12, 8, 6.6f), MapBuilder.Tex(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.5f, 0.3f, 0.3f));
            b.Box("Crate", new Vector3(-2.6f, 0.4f, 2.5f), Quaternion.Euler(0, 20, 0), Vector3.one * 0.8f, MapBuilder.Tex(TextureFactory.Wood), SurfaceType.Wood);
            b.Box("Crate2", new Vector3(-2.4f, 1.1f, 2.6f), Quaternion.Euler(0, -10, 0), Vector3.one * 0.6f, MapBuilder.Tex(TextureFactory.Wood), SurfaceType.Wood);
            b.Cylinder("Barrel", new Vector3(3.2f, 0, 3f), 0.32f, 0.95f, Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.1f, 0.08f));
            b.StreetLamp(new Vector3(4.5f, 0, 4.5f), -135f, new Color(1f, 0.55f, 0.4f), 4.5f, 14f, 3f);
            b.Finish();
            Physics.SyncTransforms();
            // Old blood on the floor and wall.
            for (int i = 0; i < 18; i++)
            {
                var p = new Vector3(Random.Range(-5f, 5f), 0.01f, Random.Range(-3f, 5.5f));
                Decals.Blood(p, Vector3.up, Random.Range(0.4f, 1.6f), Random.value < 0.3f ? 2 : 0, null);
            }
            for (int i = 0; i < 6; i++) Decals.Blood(new Vector3(Random.Range(-4f, 4f), Random.Range(0.5f, 3f), 5.99f), Vector3.back, Random.Range(0.5f, 1.4f), 1, null);

            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(root, false);
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            cam.cullingMask = ~((1 << Layers.Viewmodel) | (1 << 31));
            camGo.AddComponent<AudioListener>();

            doll = GoredollFactory.Spawn(new Vector3(0.6f, 0f, 1.2f), 200f, "Passive", 0, root);
            if (doll.Brain != null) doll.Brain.enabled = false;
            doll.DisplayName = "Menu Guy";
            UpdateCamera(0f);
        }

        /// <summary>
        /// Gore Sandbox 2 menu scene: a dim operating room. A patient lies under a blood-soaked drape under the surgical
        /// lamp, a heart monitor beeps, blood drips off the table, and the surgeon (a live Goredoll you can poke) watches you.
        /// </summary>
        void BuildOperatingRoom()
        {
            operatingRoom = true;
            var root = transform;
            MapLighting.Apply(TimeOfDay.Night, true, root);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.035f, 0.035f);
            RenderSettings.fogDensity = 0.06f;
            var b = new MapBuilder(root, null, new MapOptions());
            var tiles = MapBuilder.Tex(TextureFactory.Tiles);
            var metal = MapBuilder.Tex(TextureFactory.Metal);
            var wallC = new Color(0.45f, 0.58f, 0.55f);
            b.Block("Floor", new Vector3(-6, -0.3f, -5), new Vector3(6, 0, 7), tiles, SurfaceType.Concrete, new Color(0.38f, 0.44f, 0.43f));
            b.Block("Back Wall", new Vector3(-6, 0, 5), new Vector3(6, 4, 5.3f), tiles, SurfaceType.Concrete, wallC);
            b.Block("Left Wall", new Vector3(-6.3f, 0, -5), new Vector3(-6, 4, 5.3f), tiles, SurfaceType.Concrete, wallC);
            b.Block("Right Wall", new Vector3(6, 0, -5), new Vector3(6.3f, 4, 5.3f), tiles, SurfaceType.Concrete, wallC);
            b.Block("Ceiling", new Vector3(-6, 4, -5), new Vector3(6, 4.3f, 5.3f), tiles, SurfaceType.Concrete, new Color(0.25f, 0.28f, 0.28f));
            // Operating table along X, centre (0.4, 0, 1.6).
            var table = new Vector3(0.4f, 0f, 1.6f);
            b.Box("Table Top", table + new Vector3(0, 0.9f, 0), Quaternion.identity, new Vector3(2.0f, 0.1f, 0.62f), metal, SurfaceType.Metal, new Color(0.7f, 0.72f, 0.75f));
            b.Box("Table Post", table + new Vector3(0, 0.45f, 0), Quaternion.identity, new Vector3(0.3f, 0.9f, 0.3f), Mats.LitMetal, SurfaceType.Metal, new Color(0.5f, 0.52f, 0.55f));
            b.Box("Table Base", table + new Vector3(0, 0.04f, 0), Quaternion.identity, new Vector3(0.9f, 0.08f, 0.5f), Mats.LitMetal, SurfaceType.Metal, new Color(0.35f, 0.36f, 0.38f));
            // Surgical lamp: arm from the ceiling and a big disc with a bright face.
            b.Box("Lamp Arm", table + new Vector3(0.3f, 3.2f, 0), Quaternion.identity, new Vector3(0.06f, 1.6f, 0.06f), Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.7f, 0.72f), 1f, false);
            b.Cylinder("Lamp Head", table + new Vector3(0.3f, 2.3f, 0), 0.42f, 0.14f, Mats.LitMetal, SurfaceType.Metal, new Color(0.85f, 0.86f, 0.88f), 20);
            b.Cylinder("Lamp Face", table + new Vector3(0.3f, 2.28f, 0), 0.36f, 0.02f, Mats.Glow(new Color(1f, 0.98f, 0.92f), 3f), SurfaceType.Glass, null, 20);
            // Instrument tray, IV stand, heart monitor, cabinets, a bin of used swabs.
            b.Box("Tray", table + new Vector3(-0.2f, 1.05f, -0.75f), Quaternion.identity, new Vector3(0.55f, 0.03f, 0.35f), Mats.LitMetal, SurfaceType.Metal, new Color(0.8f, 0.81f, 0.84f));
            b.Box("Tray Leg", table + new Vector3(-0.2f, 0.52f, -0.75f), Quaternion.identity, new Vector3(0.04f, 1.04f, 0.04f), Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.6f, 0.62f), 1f, false);
            for (int k = 0; k < 6; k++)
                b.Box("Instrument", table + new Vector3(-0.42f + k * 0.08f, 1.075f, -0.75f), Quaternion.Euler(0, 10 * k - 25, 0), new Vector3(0.012f, 0.008f, 0.26f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.9f, 0.93f), 1f, false);
            b.Box("IV Pole", table + new Vector3(1.25f, 1.0f, 0.55f), Quaternion.identity, new Vector3(0.03f, 2f, 0.03f), Mats.LitMetal, SurfaceType.Metal, new Color(0.75f, 0.75f, 0.78f), 1f, false);
            b.Box("Blood Bag", table + new Vector3(1.25f, 1.85f, 0.62f), Quaternion.identity, new Vector3(0.14f, 0.24f, 0.04f), Mats.Lit, SurfaceType.Flesh, new Color(0.5f, 0.02f, 0.03f));
            b.Box("Monitor Stand", table + new Vector3(1.55f, 0.7f, -0.5f), Quaternion.identity, new Vector3(0.05f, 1.4f, 0.05f), Mats.LitMetal, SurfaceType.Metal, new Color(0.4f, 0.4f, 0.42f), 1f, false);
            b.Box("Monitor", table + new Vector3(1.55f, 1.55f, -0.5f), Quaternion.Euler(0, 150, 0), new Vector3(0.48f, 0.34f, 0.12f), Mats.Lit, SurfaceType.Metal, new Color(0.07f, 0.07f, 0.08f));
            for (int i = 0; i < 4; i++) b.Box("Cabinet", new Vector3(-4.5f + i * 1.1f, 1.0f, 4.7f), Quaternion.identity, new Vector3(1.0f, 2f, 0.5f), metal, SurfaceType.Metal, new Color(0.6f, 0.65f, 0.66f));
            b.Cylinder("Bin", new Vector3(2.6f, 0f, 2.8f), 0.22f, 0.55f, Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.12f, 0.1f), 14);
            b.Finish();
            Physics.SyncTransforms();

            var lg = new GameObject("Surgical Light").AddComponent<Light>();
            lg.transform.SetParent(root, false);
            lg.transform.position = table + new Vector3(0.3f, 2.2f, 0);
            lg.transform.rotation = Quaternion.LookRotation(Vector3.down);
            lg.type = LightType.Spot; lg.spotAngle = 70f; lg.range = 6f; lg.intensity = 3.2f; lg.color = new Color(1f, 0.97f, 0.9f);
            lg.shadows = Settings.Data.shadowQuality > 0 ? LightShadows.Soft : LightShadows.None;
            lampLight = lg;
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.transform.position = new Vector3(-3f, 3.4f, -1f);
            fill.type = LightType.Point; fill.range = 9f; fill.intensity = 0.55f; fill.color = new Color(0.55f, 0.75f, 0.8f); fill.shadows = LightShadows.None;

            // The patient: face up, head toward +X, drape to the chest.
            patient = StaticHuman.Create(root, Appearance.Random(-1), Layers.Effects, "Patient");
            patient.transform.SetPositionAndRotation(table + new Vector3(-0.88f, 1.07f, 0f), Quaternion.LookRotation(Vector3.up, Vector3.right));
            patient.Set(PartType.UpperArmL, 0f, 0f, -8f); patient.Set(PartType.UpperArmR, 0f, 0f, 8f);
            if (patient.Face != null) patient.Face.Expression = Expression.Unconscious;
            var drape = new MeshBuilder { Color = new Color(0.32f, 0.55f, 0.6f), Gloss = 0.1f };
            drape.BevelBox(new Vector3(0, 0, 0), new Vector3(1.25f, 0.05f, 0.7f), 0.02f);
            drape.SetColor(new Color(0.35f, 0.03f, 0.03f));
            drape.BevelBox(new Vector3(0.45f, 0.006f, 0.02f), new Vector3(0.32f, 0.05f, 0.4f), 0.02f); // soaked through
            var dg = new GameObject("Drape");
            dg.transform.SetParent(root, false);
            dg.transform.position = table + new Vector3(-0.2f, 1.2f, 0f);
            dg.AddComponent<MeshFilter>().sharedMesh = drape.ToMesh("Drape");
            dg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;

            // Old blood: under the table, on the drape edge, smeared on the floor and a wall.
            Decals.Blood(table + new Vector3(0.6f, 0.01f, 0.35f), Vector3.up, 1.4f, 2, null);
            for (int i = 0; i < 14; i++) Decals.Blood(new Vector3(Random.Range(-2.5f, 3f), 0.01f, Random.Range(-1f, 3.5f)), Vector3.up, Random.Range(0.2f, 0.9f), Random.value < 0.4f ? 3 : 0, null);
            for (int i = 0; i < 4; i++) Decals.Blood(new Vector3(Random.Range(-3f, 3f), Random.Range(0.4f, 2.2f), 4.99f), Vector3.back, Random.Range(0.4f, 1f), 1, null);

            // Heart monitor trace.
            var eg = new GameObject("ECG");
            eg.transform.SetParent(root, false);
            ecg = eg.AddComponent<LineRenderer>();
            ecg.positionCount = ecgBuf.Length; ecg.widthMultiplier = 0.009f; ecg.useWorldSpace = true;
            ecg.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ecg.receiveShadows = false;
            ecg.sharedMaterial = Mats.Glow(new Color(0.25f, 1f, 0.4f), 3f);
            var mrot = Quaternion.Euler(0, 150, 0);
            ecgAt = table + new Vector3(1.55f, 1.55f, -0.5f) + mrot * Vector3.back * 0.065f;
            ecgRight = mrot * Vector3.left;

            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(root, false);
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.035f, 0.035f);
            cam.cullingMask = ~((1 << Layers.Viewmodel) | (1 << 31));
            camGo.AddComponent<AudioListener>();

            // The surgeon stands on the far side of the table, watching you.
            doll = GoredollFactory.Spawn(table + new Vector3(0.1f, 0f, 0.95f), 180f, NpcTypes.Preset("doctor"), 0, root);
            if (doll.Brain != null) doll.Brain.enabled = false;
            doll.DisplayName = "The Surgeon";
            focus = table + new Vector3(0.2f, 1.2f, 0.4f);
            baseYaw = 15f; dist = 4.4f;
            UpdateCamera(0f);
        }

        // ------------------------------------------------------------------ GS2: slaughterhouse and rainy alley

        int scene; readonly System.Collections.Generic.List<Transform> hooks = new System.Collections.Generic.List<Transform>();
        ParticleSystem rain; Light alleyLamp;

        void CamAndDoll(Vector3 dollPos, float dollYaw)
        {
            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f; cam.nearClipPlane = 0.05f;
            cam.cullingMask = ~((1 << Layers.Viewmodel) | (1 << 31));
            camGo.AddComponent<AudioListener>();
            doll = GoredollFactory.Spawn(dollPos, dollYaw, "Passive", 0, transform);
            if (doll.Brain != null) doll.Brain.enabled = false;
            doll.DisplayName = "Menu Guy";
            focus = dollPos + new Vector3(0, 1.05f, 0);
            UpdateCamera(0f);
        }

        /// <summary>A dim slaughterhouse: carcasses swinging on a hook rail, tiled walls, a drain and a lot of old blood.</summary>
        void BuildSlaughterhouse()
        {
            scene = 2;
            EasterEggs.Found("Meat Locker", "Picked the slaughterhouse as your main menu.");
            var root = transform;
            MapLighting.Apply(TimeOfDay.Night, true, root);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.025f, 0.02f); RenderSettings.fogDensity = 0.07f;
            var b = new MapBuilder(root, null, new MapOptions());
            var tiles = MapBuilder.Tex(TextureFactory.Tiles);
            b.Block("Floor", new Vector3(-7, -0.3f, -5), new Vector3(7, 0, 8), MapBuilder.Tex(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.4f, 0.36f, 0.34f));
            b.Block("Back Wall", new Vector3(-7, 0, 6), new Vector3(7, 4.5f, 6.3f), tiles, SurfaceType.Concrete, new Color(0.62f, 0.6f, 0.52f));
            b.Block("Ceiling", new Vector3(-7, 4.5f, -5), new Vector3(7, 4.8f, 6.3f), tiles, SurfaceType.Concrete, new Color(0.25f, 0.24f, 0.22f));
            b.Box("Hook Rail", new Vector3(0, 3.6f, 3.6f), Quaternion.identity, new Vector3(9f, 0.08f, 0.08f), Mats.LitMetal, SurfaceType.Metal, new Color(0.45f, 0.42f, 0.4f), 1f, false);
            b.Cylinder("Drain", new Vector3(0.5f, 0.005f, 2f), 0.25f, 0.01f, Mats.LitMetal, SurfaceType.Metal, new Color(0.1f, 0.1f, 0.1f), 14, false);
            b.Box("Chopping Block", new Vector3(-2.4f, 0.45f, 1.8f), Quaternion.Euler(0, 15, 0), new Vector3(1.2f, 0.9f, 0.8f), MapBuilder.Tex(TextureFactory.Wood), SurfaceType.Wood, new Color(0.4f, 0.25f, 0.18f));
            b.Finish();
            Physics.SyncTransforms();
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("Hook").transform;
                pivot.SetParent(root, false); pivot.localPosition = new Vector3(-3.2f + i * 2.1f, 3.6f, 3.6f);
                var chain = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(chain.GetComponent<Collider>());
                chain.transform.SetParent(pivot, false); chain.transform.localPosition = new Vector3(0, -0.3f, 0); chain.transform.localScale = new Vector3(0.02f, 0.3f, 0.02f);
                chain.GetComponent<Renderer>().sharedMaterial = Mats.LitMetal;
                var body = StaticHuman.Create(pivot, Appearance.Random(-1), Layers.Effects, "Carcass");
                body.transform.localPosition = new Vector3(0, -0.6f, 0); body.transform.localRotation = Quaternion.Euler(180f, Random.Range(0f, 360f), 0);
                body.Set(PartType.UpperArmL, 0, 0, 15f); body.Set(PartType.UpperArmR, 0, 0, -15f);
                hooks.Add(pivot);
            }
            for (int i = 0; i < 22; i++) Decals.Blood(new Vector3(Random.Range(-5f, 5f), 0.01f, Random.Range(-2f, 5.5f)), Vector3.up, Random.Range(0.4f, 1.8f), Random.value < 0.35f ? 2 : 0, null);
            for (int i = 0; i < 8; i++) Decals.Blood(new Vector3(Random.Range(-5f, 5f), Random.Range(0.3f, 2.5f), 5.99f), Vector3.back, Random.Range(0.5f, 1.4f), 1, null);
            var lg = new GameObject("Bulb").AddComponent<Light>(); lg.transform.SetParent(root, false); lg.transform.localPosition = new Vector3(0.4f, 3.8f, 1.5f);
            lg.type = LightType.Point; lg.range = 9f; lg.intensity = 2.2f; lg.color = new Color(1f, 0.78f, 0.55f); lg.shadows = LightShadows.Soft;
            FlickerLight.Maybe(lg, null, 1f);
            CamAndDoll(new Vector3(0.8f, 0f, 1.4f), 200f);
            baseYaw = 200f; dist = 4.4f;
        }

        /// <summary>A narrow alley at night in the rain: wet bricks, a dumpster, a buzzing lamp, puddles of blood.</summary>
        void BuildAlley()
        {
            scene = 3;
            var root = transform;
            MapLighting.Apply(TimeOfDay.Night, false, root);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.06f); RenderSettings.fogDensity = 0.05f;
            var b = new MapBuilder(root, null, new MapOptions());
            var brick = MapBuilder.Tex(TextureFactory.Brick);
            b.Block("Ground", new Vector3(-4, -0.3f, -8), new Vector3(4, 0, 14), MapBuilder.Tex(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.22f, 0.22f, 0.24f));
            b.Block("Wall L", new Vector3(-4.3f, 0, -8), new Vector3(-4, 9, 14), brick, SurfaceType.Concrete, new Color(0.42f, 0.25f, 0.2f));
            b.Block("Wall R", new Vector3(4, 0, -8), new Vector3(4.3f, 9, 14), brick, SurfaceType.Concrete, new Color(0.38f, 0.24f, 0.2f));
            b.Box("Dumpster", new Vector3(-2.9f, 0.7f, 3.5f), Quaternion.Euler(0, 4, 0), new Vector3(1.6f, 1.4f, 2.6f), Mats.LitMetal, SurfaceType.Metal, new Color(0.15f, 0.3f, 0.2f));
            b.Box("Pipe", new Vector3(3.9f, 4f, 2f), Quaternion.identity, new Vector3(0.12f, 8f, 0.12f), Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f), 1f, false);
            b.Finish();
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) Decals.Blood(new Vector3(Random.Range(-3f, 3f), 0.01f, Random.Range(-2f, 8f)), Vector3.up, Random.Range(0.5f, 1.6f), 2, null);
            alleyLamp = new GameObject("Alley Lamp").AddComponent<Light>(); alleyLamp.transform.SetParent(root, false); alleyLamp.transform.localPosition = new Vector3(3.4f, 4.2f, 2.4f);
            alleyLamp.type = LightType.Spot; alleyLamp.spotAngle = 95f; alleyLamp.range = 14f; alleyLamp.intensity = 3f; alleyLamp.color = new Color(1f, 0.72f, 0.4f);
            alleyLamp.transform.localRotation = Quaternion.Euler(80f, -30f, 0f); alleyLamp.shadows = LightShadows.Soft;
            FlickerLight.Maybe(alleyLamp, null, 1f);
            rain = ParticleKit.Create("Menu Rain", root, Mats.Particle, 1500, 0f);
            CamAndDoll(new Vector3(0.6f, 0f, 1.6f), 200f);
            baseYaw = 200f; dist = 4.6f;
        }

        void TickScenes(float dt)
        {
            if (scene == 2)
                for (int i = 0; i < hooks.Count; i++)
                {
                    if (hooks[i] == null) continue;
                    hooks[i].localRotation = Quaternion.Euler(Mathf.Sin(Time.unscaledTime * 0.9f + i * 1.3f) * 7f, 0, Mathf.Sin(Time.unscaledTime * 0.7f + i) * 4f);
                    if (Random.value < dt * 1.2f) BloodFX.Drip(hooks[i].TransformPoint(new Vector3(0, -2.3f, 0)), Vector3.zero);
                }
            if (scene == 3 && rain != null && cam != null)
            {
                var c = cam.transform.position;
                for (int i = 0; i < 12; i++)
                    ParticleKit.Emit(rain, c + new Vector3(Random.Range(-6f, 6f), 6f, Random.Range(0f, 10f)), new Vector3(0.4f, -14f, 0.2f), Random.Range(0.012f, 0.02f), 0.6f, new Color(0.7f, 0.75f, 0.85f, 0.35f));
            }
        }

        void TickOperatingRoom(float dt)
        {
            // ECG: steady ~70 bpm with a beep.
            for (int i = 0; i < ecgBuf.Length - 1; i++) ecgBuf[i] = ecgBuf[i + 1];
            float ph = (Time.unscaledTime * 70f / 60f) % 1f, prev = ((Time.unscaledTime - dt) * 70f / 60f) % 1f;
            ecgBuf[ecgBuf.Length - 1] = Pqrst(ph);
            if (ecg != null) for (int i = 0; i < ecgBuf.Length; i++) ecg.SetPosition(i, ecgAt + ecgRight * (i / (float)(ecgBuf.Length - 1) - 0.5f) * 0.4f + Vector3.up * ecgBuf[i] * 0.1f);
            if (prev < 0.3f && ph >= 0.3f && UIRoot.Top is MainMenuScreen) AudioManager.Ui("ui_tab", 0.12f);
            // Breathing patient and a drip off the table edge.
            if (patient != null) patient.Set(PartType.Chest, Mathf.Sin(Time.unscaledTime * 1.6f) * 1.5f);
            if (Time.unscaledTime > nextDrip && patient != null)
            {
                nextDrip = Time.unscaledTime + Random.Range(0.6f, 1.4f);
                BloodFX.Drip(transform.TransformPoint(new Vector3(1.0f, 0.86f, 1.92f)), Vector3.zero);
            }
            if (lampLight != null) lampLight.intensity = 3.2f * (0.97f + 0.03f * Mathf.PerlinNoise(Time.unscaledTime * 3f, 0.3f));
        }

        static float Pqrst(float p)
        {
            float v = 0.15f * Mathf.Exp(-Mathf.Pow((p - 0.15f) / 0.035f, 2f));
            v -= 0.18f * Mathf.Exp(-Mathf.Pow((p - 0.27f) / 0.008f, 2f));
            v += Mathf.Exp(-Mathf.Pow((p - 0.3f) / 0.01f, 2f));
            v -= 0.25f * Mathf.Exp(-Mathf.Pow((p - 0.33f) / 0.01f, 2f));
            v += 0.3f * Mathf.Exp(-Mathf.Pow((p - 0.55f) / 0.06f, 2f));
            return v;
        }

        public static void TryPoke(Vector2 guiPosition)
        {
            if (current == null || current.cam == null || current.doll == null || current.pokeCooldown > 0f) return;
            var ray = current.cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 50f, 1 << Layers.Ragdoll)) return;
            var part = hit.collider.GetComponent<BodyPart>();
            if (part == null || part.Doll != current.doll) return;
            current.Poke(part, hit, ray.direction);
        }

        void Poke(BodyPart part, RaycastHit hit, Vector3 dir)
        {
            pokeCooldown = 0.25f;
            pokes++;
            part.Body.AddForceAtPosition(dir * 45f, hit.point, ForceMode.Impulse);
            AudioManager.Play("punch_hit", hit.point, 0.4f, 1.3f);
            if (doll.Vitals.Conscious) doll.Voice.Say(PokeLines[Random.Range(0, PokeLines.Length)], 1f, true);
            if (doll.Face != null) doll.Face.Expression = pokes > 4 ? Expression.Angry : Expression.Afraid;
            if (pokes % 6 == 0) doll.Ragdoll.Fall(2f);
            Stats.Add("menu_pokes");
            if (pokes == 25) EasterEggs.Found("Menu Bully", "Poked the menu Goredoll 25 times. He will remember that.");
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            pokeCooldown -= dt;
            UpdateCamera(dt);
            if (operatingRoom) TickOperatingRoom(dt);
            TickScenes(dt);
            if (doll == null) return;
            if (doll.IsDead && Time.time - doll.Vitals.TimeOfDeath > 4f) doll.Revive();
            doll.Ragdoll.LookTarget = cam.transform.position;
            if (doll.Face != null) doll.Face.LookAt(cam.transform.position);
            doll.Ragdoll.DesiredVelocity = Vector3.zero;
            doll.Ragdoll.DesiredYaw = Quaternion.LookRotation(MathX.Flat(cam.transform.position - doll.Position) + Vector3.forward * 0.001f).eulerAngles.y;

            // Small idle routine: wave, look around, dance, back to idle.
            nextIdle -= dt;
            if (nextIdle <= 0f && doll.Ragdoll.IsStanding)
            {
                nextIdle = Random.Range(5f, 10f);
                var r = Random.value;
                doll.Ragdoll.Pose = r < 0.25f ? PoseMode.Wave : r < 0.45f ? PoseMode.LookAround : r < 0.55f ? PoseMode.Dance : PoseMode.Idle;
                if (doll.Ragdoll.Pose == PoseMode.Wave) doll.Voice.Say("v_hey", 0.8f, false);
                if (doll.Ragdoll.Pose == PoseMode.Idle && Random.value < 0.4f) doll.Voice.Say(Random.value < 0.5f ? "v_hum" : "v_whistle", 0.6f, false);
                if (doll.Face != null) doll.Face.Expression = doll.Ragdoll.Pose == PoseMode.Dance ? Expression.Happy : Expression.Neutral;
            }
        }

        void UpdateCamera(float dt)
        {
            orbit += dt * 0.06f;
            // Doll on the right third of the screen, menu buttons on the left.
            float yaw = Mathf.Sin(orbit) * 12f + baseYaw;
            var offset = Quaternion.Euler(6f + Mathf.Sin(orbit * 0.7f) * 2f + (operatingRoom ? 10f : 0f), yaw, 0) * new Vector3(0, 0, dist);
            var mouse = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width) - 0.5f, Input.mousePosition.y / Mathf.Max(1, Screen.height) - 0.5f);
            cam.transform.position = transform.TransformPoint(focus) - offset + new Vector3(mouse.x * 0.25f, mouse.y * 0.15f, 0);
            cam.transform.rotation = Quaternion.LookRotation(transform.TransformPoint(focus) - cam.transform.position) * Quaternion.Euler(0, -9f, 0);
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
