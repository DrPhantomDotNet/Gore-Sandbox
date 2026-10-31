using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The trailer's film sets, all in one hidden map so nothing loads mid-trailer. Each set sits far from the others:
    /// the black void (cold open / bullet), the test lab, the blood-moon arena, a neon city street, the bank interior
    /// with its vault, the docks, and a weapon wall.
    /// </summary>
    public static class TrailerSets
    {
        public const string MapId = "trailer_studio";
        public static readonly Vector3 Void = new Vector3(0, 0, -500), Lab = Vector3.zero, Arena = new Vector3(400, 0, 0),
            City = new Vector3(0, 0, 400), Docks = new Vector3(400, 0, 400), Bank = new Vector3(-400, 0, 0), Rack = new Vector3(-400, 0, 400);

        // Things the director animates.
        public static readonly List<Light> FireLights = new List<Light>(), AlarmLights = new List<Light>(), NeonLights = new List<Light>();
        public static readonly List<Renderer> AlarmGlows = new List<Renderer>();
        public static Transform VaultDoor, BoardTip;
        public static Light VoidLight;

        public static MapDef CreateDef() => new MapDef
        {
            Id = MapId, Name = "Trailer", Size = "Studio", Glyph = "camera", DefaultTime = TimeOfDay.Night, Ambience = null,
            Description = "SolarEclipse Productions trailer studio.", Build = Build, NavCell = 2f, Secret = true,
        };

        static Material M(Texture2D t) => MapBuilder.Tex(t);

        static void Build(MapBuilder m)
        {
            FireLights.Clear(); AlarmLights.Clear(); NeonLights.Clear(); AlarmGlows.Clear();
            m.Options.StartingNpcs = 0; m.Options.Nextbots = false;
            BuildVoid(m); BuildLab(m); BuildArena(m); BuildCity(m); BuildBank(m); BuildDocks(m); BuildRack(m);
            m.PlayerSpawn = Void + new Vector3(0, 0.2f, -20f);
            m.PlayerYaw = 0f;
            m.Playable = new Bounds(Void + new Vector3(0, 2, -20), new Vector3(10, 4, 10));
            m.KillY = -200f;
            if (TrailerDirector.Pending) m.Root.gameObject.AddComponent<TrailerDirector>();
        }

        // ------------------------------------------------------------------ void

        static void BuildVoid(MapBuilder m)
        {
            m.Block("Void Floor", Void + new Vector3(-40, -1, -40), Void + new Vector3(40, 0, 40), Mats.Lit, SurfaceType.Concrete, new Color(0.02f, 0.02f, 0.025f));
            var go = new GameObject("Void Light");
            go.transform.SetParent(m.Root, false);
            go.transform.position = Void + new Vector3(0, 6f, 0);
            VoidLight = go.AddComponent<Light>();
            VoidLight.type = LightType.Point; VoidLight.color = new Color(1f, 0.1f, 0.08f); VoidLight.range = 14f; VoidLight.intensity = 3f;
        }

        // ------------------------------------------------------------------ lab

        static void BuildLab(MapBuilder m)
        {
            var c = Lab;
            m.Block("Lab Floor", c + new Vector3(-60, -1, -60), c + new Vector3(60, 0, 60), M(TextureFactory.Tiles), SurfaceType.Concrete, new Color(0.78f, 0.8f, 0.82f), 0.25f);
            for (int i = -5; i <= 5; i++)
            {
                m.Box("Grid", c + new Vector3(i * 10f, 0.01f, 0), Quaternion.identity, new Vector3(0.08f, 0.01f, 110f), Mats.Lit, SurfaceType.Concrete, new Color(0.3f, 0.6f, 0.9f), 1f, false);
                m.Box("Grid", c + new Vector3(0, 0.01f, i * 10f), Quaternion.identity, new Vector3(110f, 0.01f, 0.08f), Mats.Lit, SurfaceType.Concrete, new Color(0.3f, 0.6f, 0.9f), 1f, false);
            }
            m.Ramp("Ramp", c + new Vector3(-20, 0, -10), 0f, 6f, 3f, 18f, M(TextureFactory.Wood));
            m.Ramp("Ramp", c + new Vector3(-10, 0, -10), 0f, 6f, 5f, 28f, M(TextureFactory.Wood));
            // Tower + diving board over the spike pit
            m.Block("Tower", c + new Vector3(8, 0, 8), c + new Vector3(12, 10, 12), M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.75f, 0.75f, 0.78f));
            m.Stairs("Tower Stairs", c + new Vector3(13, 0, 8), 0f, 2f, 20, 0.5f, 0.6f, M(TextureFactory.Metal), SurfaceType.Metal);
            m.Box("Diving Board", c + new Vector3(10, 10.1f, 14.5f), Quaternion.identity, new Vector3(0.8f, 0.15f, 5f), M(TextureFactory.Wood), SurfaceType.Wood, new Color(0.7f, 0.5f, 0.3f));
            var tip = new GameObject("Board Tip").transform; tip.SetParent(m.Root, false); tip.position = c + new Vector3(10, 10.2f, 16.6f); BoardTip = tip;
            m.Block("Pit Wall", c + new Vector3(6, 0, 15), c + new Vector3(14, 0.8f, 15.3f), M(TextureFactory.Metal), SurfaceType.Metal, new Color(0.5f, 0.52f, 0.55f));
            m.Block("Pit Wall", c + new Vector3(6, 0, 22.7f), c + new Vector3(14, 0.8f, 23f), M(TextureFactory.Metal), SurfaceType.Metal, new Color(0.5f, 0.52f, 0.55f));
            SpikeTrap.Create(m.Root, c + new Vector3(10, 0.05f, 19f), new Vector2(7f, 7f));
            BladeFan.Create(m.Root, c + new Vector3(-22, 1.8f, 14), Quaternion.Euler(0, 90, 0), 1.6f);
            m.Block("Fan Frame", c + new Vector3(-22.4f, 0, 11.8f), c + new Vector3(-21.6f, 3.8f, 12.2f), M(TextureFactory.Metal), SurfaceType.Metal, new Color(0.9f, 0.75f, 0.1f));
            m.Block("Fan Frame", c + new Vector3(-22.4f, 0, 15.8f), c + new Vector3(-21.6f, 3.8f, 16.2f), M(TextureFactory.Metal), SurfaceType.Metal, new Color(0.9f, 0.75f, 0.1f));
            for (int k = 0; k < 8; k++) PropFactory.Spawn(k % 3 == 0 ? "explosive_barrel" : "crate", c + new Vector3(-30 + (k % 4) * 2.2f, 0.6f, -24 + (k / 4) * 2.2f), Quaternion.Euler(0, k * 25, 0));
            for (int k = 0; k < 10; k++) m.Tree(c + new Vector3(-55 + k * 12, 0, 55 - (k % 2) * 6), 1.2f, k % 2 == 0);
        }

        // ------------------------------------------------------------------ arena

        static void BuildArena(MapBuilder m)
        {
            var c = Arena;
            m.Block("Arena Ground", c + new Vector3(-80, -1, -80), c + new Vector3(80, 0, 80), M(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.25f, 0.15f, 0.12f));
            m.Cylinder("Arena Sand", c + Vector3.up * 0.0f, 24f, 0.12f, M(TextureFactory.Sand), SurfaceType.Dirt, new Color(0.75f, 0.55f, 0.42f), 48);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                m.Box("Arena Wall", c + dir * 25f + Vector3.up * 1.6f, Quaternion.Euler(0, a, 0), new Vector3(6.6f, 3.2f, 0.8f), M(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.45f, 0.2f, 0.17f));
                if (i % 2 == 0) m.Stairs("Bleachers", c + dir * 26f, a, 6f, 6, 0.6f, 0.9f, M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.35f, 0.3f, 0.3f));
                if (i % 4 == 0)
                {
                    var bowl = c + dir * 21f;
                    m.Cylinder("Fire Bowl Stand", bowl, 0.25f, 1.6f, Mats.LitMetal, SurfaceType.Metal, new Color(0.2f, 0.2f, 0.2f), 8);
                    m.Cylinder("Fire Bowl", bowl + Vector3.up * 1.6f, 0.7f, 0.35f, Mats.LitMetal, SurfaceType.Metal, new Color(0.15f, 0.12f, 0.1f), 12);
                    m.Cylinder("Fire", bowl + Vector3.up * 1.95f, 0.55f, 0.5f, Mats.Glow(new Color(1f, 0.45f, 0.1f), 3f), SurfaceType.Metal, Color.white, 10, false);
                    var l = new GameObject("Fire Light").AddComponent<Light>();
                    l.transform.SetParent(m.Root, false); l.transform.position = bowl + Vector3.up * 2.6f;
                    l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.15f); l.range = 16f; l.intensity = 2.4f;
                    FireLights.Add(l);
                }
            }
            for (int k = 0; k < 3; k++)
            {
                var cg = c + Quaternion.Euler(0, 40 + k * 110, 0) * Vector3.forward * 17f;
                for (int b = 0; b < 8; b++)
                {
                    var off = Quaternion.Euler(0, b * 45, 0) * Vector3.forward * 1.3f;
                    m.Box("Cage Bar", cg + off + Vector3.up * 1.4f, Quaternion.identity, new Vector3(0.08f, 2.8f, 0.08f), Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f));
                }
                m.Cylinder("Cage Top", cg + Vector3.up * 2.8f, 1.4f, 0.1f, Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f), 12);
            }
        }

        // ------------------------------------------------------------------ city street (sunset neon)

        static void BuildCity(MapBuilder m)
        {
            var c = City;
            m.Block("City Ground", c + new Vector3(-150, -1, -150), c + new Vector3(150, 0, 150), M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.5f, 0.5f, 0.5f));
            m.Block("Road", c + new Vector3(-150, 0, -7), c + new Vector3(150, 0.02f, 7), M(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.38f, 0.38f, 0.4f), 0.2f, false);
            m.Block("Cross Road", c + new Vector3(30, 0, -150), c + new Vector3(44, 0.021f, 150), M(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.38f, 0.38f, 0.4f), 0.2f, false);
            for (float x = -146; x < 150; x += 8f)
                if (Mathf.Abs(x - 37) > 9) m.Box("Lane", c + new Vector3(x, 0.03f, 0), Quaternion.identity, new Vector3(3f, 0.01f, 0.2f), Mats.Lit, SurfaceType.Concrete, new Color(0.95f, 0.85f, 0.3f), 1f, false);
            m.Block("Sidewalk N", c + new Vector3(-150, 0, 7), c + new Vector3(150, 0.18f, 12), M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.65f, 0.65f, 0.63f));
            m.Block("Sidewalk S", c + new Vector3(-150, 0, -12), c + new Vector3(150, 0.18f, -7), M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.65f, 0.65f, 0.63f));
            Color[] neon = { new Color(1f, 0.2f, 0.7f), new Color(0.1f, 0.9f, 1f), new Color(0.6f, 0.3f, 1f), new Color(1f, 0.55f, 0.1f) };
            var rnd = new System.Random(77);
            for (int i = 0; i < 12; i++)
            {
                float x = -140 + i * 24;
                if (Mathf.Abs(x + 4) < 14) continue; // the bank gets this spot
                float h = 18 + (float)rnd.NextDouble() * 40;
                if (Mathf.Abs(x - 37) > 12)
                {
                    m.Building(c + new Vector3(x - 10, 0.18f, 12.5f), c + new Vector3(x + 10, h, 34), TextureFactory.Concrete, new Color(0.4f + (float)rnd.NextDouble() * 0.2f, 0.4f, 0.48f), true);
                    m.Building(c + new Vector3(x - 10, 0.18f, -34), c + new Vector3(x + 10, h * 0.8f, -12.5f), TextureFactory.Brick, new Color(0.5f, 0.38f, 0.35f), true);
                }
                var col = neon[i % neon.Length];
                m.Box("Neon Sign", c + new Vector3(x, 5.5f + (i % 3), 12.3f), Quaternion.identity, new Vector3(6f, 0.8f, 0.1f), Mats.Glow(col, 3f), SurfaceType.Glass, Color.white, 1f, false);
                m.Box("Neon Sign", c + new Vector3(x + 3, 4f, -12.3f), Quaternion.identity, new Vector3(0.6f, 3f, 0.1f), Mats.Glow(neon[(i + 1) % neon.Length], 3f), SurfaceType.Glass, Color.white, 1f, false);
                var l = new GameObject("Neon Light").AddComponent<Light>();
                l.transform.SetParent(m.Root, false); l.transform.position = c + new Vector3(x, 5f, 10.5f);
                l.type = LightType.Point; l.color = col; l.range = 10f; l.intensity = 1.6f;
                NeonLights.Add(l);
                if (i % 2 == 0) m.StreetLamp(c + new Vector3(x + 6, 0.18f, -8f), 0f, new Color(1f, 0.8f, 0.55f), 6f, 18f, 1.8f);
            }
            // The bank (north side, centre)
            var b = c + new Vector3(-4, 0, 12.5f);
            m.Block("Bank", b + new Vector3(-13, 0.18f, 0), b + new Vector3(13, 26, 22), M(TextureFactory.Plaster), SurfaceType.Concrete, new Color(0.88f, 0.86f, 0.8f));
            m.Block("Bank Steps", b + new Vector3(-10, 0.18f, -3f), b + new Vector3(10, 0.5f, 0), M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.85f, 0.85f, 0.82f));
            for (int k = -2; k <= 2; k++) m.Cylinder("Pillar", b + new Vector3(k * 4.4f, 0.5f, -1.4f), 0.6f, 8.5f, M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.95f, 0.92f, 0.85f), 14);
            m.Box("Bank Roof Edge", b + new Vector3(0, 9.2f, -1.4f), Quaternion.identity, new Vector3(24f, 1.2f, 3.2f), M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.92f, 0.9f, 0.84f));
            m.Box("Bank Sign", b + new Vector3(0, 10.6f, -0.1f), Quaternion.identity, new Vector3(14f, 1.6f, 0.15f), Mats.Glow(new Color(1f, 0.82f, 0.3f), 3f), SurfaceType.Glass, Color.white, 1f, false);
            m.Box("Bank Doors", b + new Vector3(0, 2.2f, -0.05f), Quaternion.identity, new Vector3(5f, 4f, 0.1f), Mats.Glow(new Color(1f, 0.8f, 0.45f), 1.2f), SurfaceType.Glass, Color.white, 1f, false);
        }

        // ------------------------------------------------------------------ bank interior

        static void BuildBank(MapBuilder m)
        {
            var c = Bank;
            m.Block("Bank Floor", c + new Vector3(-20, -1, -16), c + new Vector3(20, 0, 20), M(TextureFactory.Tiles), SurfaceType.Concrete, new Color(0.92f, 0.9f, 0.86f), 0.4f);
            m.Block("Bank Ceiling", c + new Vector3(-20, 8, -16), c + new Vector3(20, 8.5f, 20), M(TextureFactory.Ceiling), SurfaceType.Concrete, Color.white);
            m.Block("Wall", c + new Vector3(-20, 0, -16), c + new Vector3(-19.5f, 8, 20), M(TextureFactory.Plaster), SurfaceType.Concrete, new Color(0.85f, 0.8f, 0.7f));
            m.Block("Wall", c + new Vector3(19.5f, 0, -16), c + new Vector3(20, 8, 20), M(TextureFactory.Plaster), SurfaceType.Concrete, new Color(0.85f, 0.8f, 0.7f));
            m.Block("Wall", c + new Vector3(-20, 0, -16.5f), c + new Vector3(20, 8, -16), M(TextureFactory.Plaster), SurfaceType.Concrete, new Color(0.85f, 0.8f, 0.7f));
            for (int k = -2; k <= 2; k++) m.Cylinder("Pillar", c + new Vector3(k * 7f, 0, -4), 0.7f, 8f, M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.95f, 0.93f, 0.88f), 14);
            m.Block("Teller Counter", c + new Vector3(-14, 0, 2), c + new Vector3(-3, 1.2f, 3.2f), M(TextureFactory.Wood), SurfaceType.Wood, new Color(0.45f, 0.28f, 0.16f));
            m.Block("Teller Counter", c + new Vector3(3, 0, 2), c + new Vector3(14, 1.2f, 3.2f), M(TextureFactory.Wood), SurfaceType.Wood, new Color(0.45f, 0.28f, 0.16f));
            // Vault wall with a round door that swings open (hinged on its left edge).
            m.Block("Vault Wall", c + new Vector3(-20, 0, 10), c + new Vector3(-3.2f, 8, 11), M(TextureFactory.Metal), SurfaceType.Metal, new Color(0.45f, 0.47f, 0.5f));
            m.Block("Vault Wall", c + new Vector3(3.2f, 0, 10), c + new Vector3(20, 8, 11), M(TextureFactory.Metal), SurfaceType.Metal, new Color(0.45f, 0.47f, 0.5f));
            m.Block("Vault Wall", c + new Vector3(-3.2f, 6.2f, 10), c + new Vector3(3.2f, 8, 11), M(TextureFactory.Metal), SurfaceType.Metal, new Color(0.45f, 0.47f, 0.5f));
            var hinge = new GameObject("Vault Hinge").transform;
            hinge.SetParent(m.Root, false);
            hinge.position = c + new Vector3(-3.1f, 3.1f, 10f);
            var door = new GameObject("Vault Door");
            door.transform.SetParent(hinge, false);
            door.transform.localPosition = new Vector3(3.1f, 0, -0.2f);
            var mb = new MeshBuilder { Color = new Color(0.62f, 0.63f, 0.66f), Gloss = 0.9f };
            mb.Cylinder(new Vector3(0, 0, -0.35f), new Vector3(0, 0, 0.35f), 3f, 3f, 32);
            mb.SetColor(new Color(0.8f, 0.8f, 0.84f), 0.95f);
            mb.Torus(new Vector3(0, 0, -0.4f), 1.2f, 0.12f, 20, 8, Vector3.forward);
            for (int k = 0; k < 6; k++) mb.Box(Quaternion.Euler(0, 0, k * 30f) * Vector3.zero + new Vector3(0, 0, -0.45f), new Vector3(0.12f, 2.4f, 0.1f));
            door.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("VaultDoor");
            door.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            VaultDoor = hinge;
            // Gold inside
            for (int s = 0; s < 4; s++)
                for (int k = 0; k < 10; k++)
                    m.Box("Gold", c + new Vector3(-6 + (k % 5) * 3f, 0.3f + s * 0.32f + (k / 5) * 1.6f, 13f + (k / 5) * 2.5f), Quaternion.Euler(0, (k * 13) % 20 - 10, 0), new Vector3(1.6f, 0.3f, 0.7f), Mats.Glow(new Color(1f, 0.78f, 0.2f), 0.7f), SurfaceType.Metal, Color.white, 1f, false);
            m.Block("Vault Floor", c + new Vector3(-10, 0, 11), c + new Vector3(10, 0.01f, 20), Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f), 0.5f, false);
            m.CeilingLight(c + new Vector3(0, 7.6f, 15), new Color(1f, 0.9f, 0.6f), 12f, 1.6f);
            m.CeilingLight(c + new Vector3(0, 7.6f, -6), new Color(1f, 0.95f, 0.85f), 20f, 1.4f);
            // Alarm lights
            for (int k = 0; k < 4; k++)
            {
                var p = c + new Vector3(-15 + k * 10, 7.3f, 9.4f);
                var g = m.Box("Alarm", p, Quaternion.identity, new Vector3(0.6f, 0.4f, 0.3f), Mats.Glow(new Color(1f, 0.05f, 0.05f), 4f), SurfaceType.Glass, Color.white, 1f, false);
                AlarmGlows.Add(g.GetComponent<Renderer>());
                var l = new GameObject("Alarm Light").AddComponent<Light>();
                l.transform.SetParent(m.Root, false); l.transform.position = p + Vector3.back * 0.6f;
                l.type = LightType.Point; l.color = new Color(1f, 0.05f, 0.05f); l.range = 18f; l.intensity = 0f;
                AlarmLights.Add(l);
            }
        }

        // ------------------------------------------------------------------ docks (blue night)

        static void BuildDocks(MapBuilder m)
        {
            var c = Docks;
            m.Block("Water", c + new Vector3(-150, -2.2f, 20), c + new Vector3(150, -1.8f, 150), Mats.Lit, SurfaceType.Glass, new Color(0.02f, 0.06f, 0.1f), 1f, false);
            m.Block("Quay", c + new Vector3(-80, -2, -60), c + new Vector3(80, 0, 20), M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.45f, 0.47f, 0.5f));
            Color[] paint = { new Color(0.62f, 0.16f, 0.12f), new Color(0.15f, 0.4f, 0.22f), new Color(0.75f, 0.5f, 0.12f), new Color(0.2f, 0.25f, 0.45f), new Color(0.5f, 0.5f, 0.52f) };
            var rnd = new System.Random(5);
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 7; k++)
                {
                    if (row == 1 && (k == 3 || k == 4)) continue; // the fight lane
                    var p = c + new Vector3(-30 + k * 9f, 0, -30 + row * 14f);
                    int h = 1 + rnd.Next(3);
                    for (int s = 0; s < h; s++)
                        m.Box("Container", p + Vector3.up * (1.3f + s * 2.6f), Quaternion.Euler(0, 90, 0), new Vector3(2.44f, 2.6f, 6.06f), Mats.LitMetal, SurfaceType.Metal, paint[(k + s + row) % paint.Length]);
                }
            // Crane
            m.Block("Crane Leg", c + new Vector3(30, 0, 8), c + new Vector3(31.5f, 26, 9.5f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.6f, 0.1f));
            m.Block("Crane Leg", c + new Vector3(30, 0, 17), c + new Vector3(31.5f, 26, 18.5f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.6f, 0.1f));
            m.Block("Crane Arm", c + new Vector3(29, 26, 6), c + new Vector3(32.5f, 28, 50), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.6f, 0.1f));
            // Boat
            m.Box("Boat Hull", c + new Vector3(10, -1.0f, 30), Quaternion.identity, new Vector3(4.5f, 1.6f, 11f), Mats.LitMetal, SurfaceType.Metal, new Color(0.92f, 0.92f, 0.95f), 1f, false, false);
            for (int k = 0; k < 4; k++) m.StreetLamp(c + new Vector3(-36 + k * 22, 0, 16), 180f, new Color(1f, 0.7f, 0.4f), 8f, 22f, 1.6f);
            var moon = new GameObject("Moon Fill").AddComponent<Light>();
            moon.transform.SetParent(m.Root, false); moon.transform.position = c + new Vector3(0, 14, -10);
            moon.type = LightType.Point; moon.color = new Color(0.35f, 0.55f, 1f); moon.range = 45f; moon.intensity = 1.3f;
        }

        // ------------------------------------------------------------------ weapon wall

        static void BuildRack(MapBuilder m)
        {
            var c = Rack;
            m.Block("Rack Floor", c + new Vector3(-20, -1, -20), c + new Vector3(20, 0, 20), Mats.Lit, SurfaceType.Concrete, new Color(0.05f, 0.05f, 0.06f));
            m.Block("Rack Wall", c + new Vector3(-12, 0, 6), c + new Vector3(12, 9, 6.5f), M(TextureFactory.Metal), SurfaceType.Metal, new Color(0.12f, 0.12f, 0.14f));
            int i = 0;
            foreach (var def in WeaponCatalog.All)
            {
                if (def == null || def.Kind == WeaponKind.Fists || def.Kind == WeaponKind.Admin || def.Id == "spawngun" || def.Id == "gravitygun" || def.Id == "ropetool") continue;
                if (i >= 30) break;
                WeaponModel w = null;
                try { w = WeaponModels.Build(def.Id, 0); } catch (System.Exception) { }
                if (w == null) continue;
                w.transform.SetParent(m.Root, false);
                w.transform.position = c + new Vector3(-9 + (i % 6) * 3.6f, 1.2f + (i / 6) * 1.5f, 5.9f);
                w.transform.rotation = Quaternion.Euler(0, 90, 0);
                foreach (var b in w.GetComponentsInChildren<Behaviour>()) b.enabled = false;
                i++;
            }
            for (int k = 0; k < 3; k++)
            {
                var l = new GameObject("Rack Light").AddComponent<Light>();
                l.transform.SetParent(m.Root, false); l.transform.position = c + new Vector3(-7 + k * 7, 5, 2);
                l.type = LightType.Point; l.color = new Color(0.3f, 0.85f, 1f); l.range = 12f; l.intensity = 2f;
            }
        }
    }
}
