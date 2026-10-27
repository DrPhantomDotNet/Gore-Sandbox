using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// "Rooftops": six skyscraper tops at different heights (34-52 m) joined by metal bridges, wooden planks and a
    /// stair run, with AC units, water towers, antennas, a neon billboard, crates and a helipad. Every edge is a
    /// lethal drop to the street far below (cars, lamps, dumpsters). A night skyline of lit blocks surrounds it.
    /// The roofs are flat solid blocks, so the NavGrid samples them as walkable islands; bridges are at most ~30
    /// degrees and overlap both roofs, so the AI can path between roofs (a 0.5 m step limit keeps it off the edges).
    /// </summary>
    public static class RooftopsMap
    {
        public const string MapId = "rooftops";

        struct Roof
        {
            public Vector3 Min, Max; // Max.y = roof top
            public Roof(float x0, float z0, float x1, float z1, float top) { Min = new Vector3(x0, 0, z0); Max = new Vector3(x1, top, z1); }
            public Vector3 Top(float x, float z) => new Vector3(x, Max.y, z);
        }

        // Roof layout (top view, metres). Gaps between roofs are the streets/alleys below.
        static readonly Roof B1 = new Roof(-30, -30, -8, -8, 40);   // spawn roof
        static readonly Roof B2 = new Roof(0, -28, 18, -10, 44);    // billboard roof
        static readonly Roof B3 = new Roof(-30, 0, -10, 22, 34);    // water tower roof
        static readonly Roof B4 = new Roof(6, 4, 26, 26, 52);       // helipad tower
        static readonly Roof B5 = new Roof(-6, 6, 2, 18, 34);       // small annex
        static readonly Roof B6 = new Roof(28, -26, 42, -8, 39);    // far roof

        public static MapDef CreateDef() => new MapDef
        {
            Id = MapId, Name = "Rooftops", Size = "Medium - city rooftops", Glyph = "home", DefaultTime = TimeOfDay.Night, Ambience = "amb_city",
            Description = "Skyscraper roofs high above a city at night, connected by bridges, planks and stairs. AC units, water towers, antennas and a helipad - and a very long way down to the street.",
            Features = new[] { "Lethal drops", "Bridges & planks", "Helipad", "Water towers", "Night skyline", "Throw them off" },
            Tags = new[] { "High Places" },
            ThemeTop = new Color(0.03f, 0.04f, 0.12f), ThemeBottom = new Color(0.35f, 0.22f, 0.32f), Accent = new Color(1f, 0.75f, 0.35f),
            Build = Build,
        };

        static void Build(MapBuilder m)
        {
            // Keep the distant skyline visible: night fog would swallow it.
            RenderSettings.fogDensity = Mathf.Min(RenderSettings.fogDensity, 0.0055f);

            var asphalt = MapBuilder.Tex(TextureFactory.Asphalt);
            var walk = MapBuilder.Tex(TextureFactory.Tiles);
            var conc = MapBuilder.Tex(TextureFactory.Concrete);
            var wood = MapBuilder.Tex(TextureFactory.Wood);
            var metal = MapBuilder.Tex(TextureFactory.Metal);

            // ---- Street level
            m.Block("Street", new Vector3(-200, -1, -200), new Vector3(200, 0, 200), asphalt, SurfaceType.Concrete, new Color(0.6f, 0.6f, 0.62f), 0.2f);
            var lit = new MeshBuilder { Color = Color.white, Gloss = 0.6f };
            var dark = new MeshBuilder { Color = new Color(0.07f, 0.09f, 0.13f), Gloss = 0.7f };
            Texture2D[] walls = { TextureFactory.Brick, TextureFactory.Concrete, TextureFactory.Plaster };
            Color[] tints = { new Color(0.55f, 0.45f, 0.42f), new Color(0.5f, 0.52f, 0.56f), new Color(0.62f, 0.58f, 0.5f), new Color(0.42f, 0.44f, 0.5f) };
            var roofs = new[] { B1, B2, B3, B4, B5, B6 };
            for (int i = 0; i < roofs.Length; i++)
            {
                var r = roofs[i];
                m.Block("Sidewalk", new Vector3(r.Min.x - 2f, 0, r.Min.z - 2f), new Vector3(r.Max.x + 2f, 0.15f, r.Max.z + 2f), walk, SurfaceType.Concrete, new Color(0.7f, 0.7f, 0.7f), 0.5f);
                m.Block("Tower", r.Min, new Vector3(r.Max.x, r.Max.y - 0.25f, r.Max.z), MapBuilder.Tex(walls[i % walls.Length]), SurfaceType.Concrete, tints[i % tints.Length], 0.25f);
                m.Block("Roof", new Vector3(r.Min.x, r.Max.y - 0.25f, r.Min.z), r.Max, conc, SurfaceType.Concrete, new Color(0.5f, 0.5f, 0.52f), 0.5f);
                Windows(lit, dark, r.Min, new Vector3(r.Max.x, r.Max.y - 1.5f, r.Max.z), 1.3f, 1.7f, 2.6f, 3.4f, 0.4f, true);
            }

            // ---- Connections (all overlap the roofs; slopes <= ~30 degrees)
            var railTint = new Color(0.3f, 0.32f, 0.34f);
            var plankTint = new Color(0.75f, 0.6f, 0.42f);
            // B1 -> B2: metal bridge, 40 -> 44 m.
            Plank(m, B1.Top(-8.6f, -19f), B2.Top(0f, -19f), 2.4f, metal, SurfaceType.Metal, railTint, true);
            // B2 -> B4: long metal bridge up to the helipad tower, 44 -> 52 m.
            Plank(m, B2.Top(12f, -10.6f), B4.Top(12f, 4f), 2.4f, metal, SurfaceType.Metal, railTint, true);
            // B2 -> B6: sketchy wooden plank down to the far roof, 44 -> 39 m.
            Plank(m, B6.Top(28.6f, -17f), B2.Top(18f, -17f), 1.3f, wood, SurfaceType.Wood, plankTint, false);
            // B3 <-> B5: flat plank across an alley at 34 m.
            Plank(m, B3.Top(-10.6f, 12f), B5.Top(-5.4f, 12f), 1.4f, wood, SurfaceType.Wood, plankTint, false);
            Plank(m, B3.Top(-10.6f, 16.5f), B5.Top(-5.4f, 16.5f), 1.4f, wood, SurfaceType.Wood, plankTint * 0.85f, false);
            // B3 -> B1: steel stairs climbing 6 m over the alley (z 0 -> -8).
            m.Stairs("Roof Stairs", B3.Top(-20f, 0.4f), 180f, 2.2f, 15, 0.4f, 8.4f / 15f, metal, SurfaceType.Metal, railTint);
            m.Box("Stair Rail", new Vector3(-21.15f, 37.9f, -3.8f), Quaternion.Euler(-35.5f, 180f, 0), new Vector3(0.06f, 0.06f, 10.2f), Mats.LitMetal, SurfaceType.Metal, railTint);
            m.Box("Stair Rail", new Vector3(-18.85f, 37.9f, -3.8f), Quaternion.Euler(-35.5f, 180f, 0), new Vector3(0.06f, 0.06f, 10.2f), Mats.LitMetal, SurfaceType.Metal, railTint);

            // ---- Parapets: a low curb (0.35 m, under the AI step limit) with gaps where the connections land.
            var gaps = new List<Vector3>
            {
                new Vector3(-8, 0, -19), new Vector3(0, 0, -19), new Vector3(12, 0, -10), new Vector3(12, 0, 4), new Vector3(28, 0, -17),
                new Vector3(18, 0, -17), new Vector3(-10, 0, 12), new Vector3(-6, 0, 12), new Vector3(-10, 0, 16.5f), new Vector3(-6, 0, 16.5f),
                new Vector3(-20, 0, 0), new Vector3(-20, 0, -8),
            };
            foreach (var r in roofs) Parapet(m, r, gaps, conc);

            // ---- Roof furniture
            // B1 (spawn): AC units, crates, a roof access hut, lamp.
            AcUnit(m, new Vector3(-26, 40, -26), 0); AcUnit(m, new Vector3(-23.5f, 40, -26), 0); AcUnit(m, new Vector3(-12, 40, -27), 90);
            m.Block("Access Hut", new Vector3(-29, 40, -14), new Vector3(-24, 43f, -10), conc, SurfaceType.Concrete, new Color(0.55f, 0.53f, 0.5f));
            m.Block("Hut Roof", new Vector3(-29.2f, 43f, -14.2f), new Vector3(-23.8f, 43.2f, -9.8f), Mats.LitMetal, SurfaceType.Metal, railTint);
            m.Box("Hut Door", new Vector3(-24f + 0.03f, 41.1f, -12f), Quaternion.identity, new Vector3(0.08f, 2.2f, 1.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.35f, 0.25f, 0.2f), 1, false);
            m.StreetLamp(new Vector3(-24f, 40, -12f), 90f, new Color(1f, 0.85f, 0.6f), 3.2f, 14f, 1.8f);
            Antenna(m, new Vector3(-28, 43.2f, -12), 9f);

            // B2: neon billboard on legs facing the spawn roof, AC units.
            var bb = new Vector3(14f, 44, -24f);
            for (int i = 0; i < 3; i++) m.Cylinder("Billboard Leg", bb + new Vector3(0, 0, -3f + i * 3f), 0.12f, 6f, Mats.LitMetal, SurfaceType.Metal, railTint, 8);
            m.Box("Billboard", bb + new Vector3(0, 7.5f, 0), Quaternion.identity, new Vector3(0.4f, 3.2f, 8f), Mats.LitMetal, SurfaceType.Metal, new Color(0.2f, 0.2f, 0.22f));
            m.Box("Billboard Neon", bb + new Vector3(-0.22f, 7.5f, 0), Quaternion.identity, new Vector3(0.05f, 2.8f, 7.6f), Mats.Glow(new Color(1f, 0.25f, 0.55f), 2.2f), SurfaceType.Glass, Color.white, 1, false);
            m.Box("Billboard Neon Text", bb + new Vector3(-0.26f, 7.6f, 0), Quaternion.identity, new Vector3(0.04f, 0.6f, 5.5f), Mats.Glow(new Color(0.3f, 0.9f, 1f), 2.5f), SurfaceType.Glass, Color.white, 1, false);
            AcUnit(m, new Vector3(4, 44, -14), 0); AcUnit(m, new Vector3(4, 44, -25), 90);
            m.StreetLamp(new Vector3(9f, 44, -27f), 0f, new Color(1f, 0.85f, 0.6f), 3.2f, 14f, 1.8f);

            // B3: water tower, skylights, AC.
            WaterTower(m, new Vector3(-24f, 34, 16f));
            m.Block("Skylight", new Vector3(-18, 34, 6), new Vector3(-14, 34.6f, 10), Mats.Glass, SurfaceType.Glass, Color.white);
            AcUnit(m, new Vector3(-27, 34, 4), 90); AcUnit(m, new Vector3(-14, 34, 19), 0);
            m.StreetLamp(new Vector3(-12f, 34, 4f), 180f, new Color(0.7f, 0.85f, 1f), 3.2f, 14f, 1.6f);

            // B4: helipad tower.
            var hp = B4.Top(16f, 15f);
            m.Cylinder("Helipad", hp + Vector3.up * 0.01f, 7f, 0.05f, Mats.LitMetal, SurfaceType.Concrete, new Color(0.22f, 0.23f, 0.25f), 32, false);
            var white = Mats.Glow(new Color(0.95f, 0.95f, 0.9f), 0.9f);
            m.Box("Helipad H", hp + new Vector3(-1.6f, 0.07f, 0), Quaternion.identity, new Vector3(0.6f, 0.02f, 4.4f), white, SurfaceType.Concrete, Color.white, 1, false);
            m.Box("Helipad H", hp + new Vector3(1.6f, 0.07f, 0), Quaternion.identity, new Vector3(0.6f, 0.02f, 4.4f), white, SurfaceType.Concrete, Color.white, 1, false);
            m.Box("Helipad H", hp + new Vector3(0, 0.07f, 0), Quaternion.identity, new Vector3(2.6f, 0.02f, 0.6f), white, SurfaceType.Concrete, Color.white, 1, false);
            var amber = Mats.Glow(new Color(1f, 0.6f, 0.15f), 2.5f);
            for (int i = 0; i < 16; i++)
            {
                var d = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward * 6.6f;
                m.Box("Helipad Light", hp + d + Vector3.up * 0.08f, Quaternion.identity, new Vector3(0.18f, 0.08f, 0.18f), amber, SurfaceType.Glass, Color.white, 1, false);
            }
            m.CeilingLight(hp + new Vector3(0, 0.15f, -7.8f), new Color(1f, 0.75f, 0.45f), 18f, 1.6f, new Vector3(0.6f, 0.15f, 0.3f));
            Antenna(m, B4.Top(24f, 24f), 14f);
            Antenna(m, B4.Top(8f, 24.5f), 8f);
            AcUnit(m, B4.Top(23f, 6.5f), 0);

            // B5: second water tower and a radio.
            WaterTower(m, new Vector3(-2f, 34, 9.5f));
            Radio.Create(m.Root, new Vector3(0.5f, 34.02f, 16f), 200f);

            // B6: AC cluster, a bell and a lamp.
            AcUnit(m, new Vector3(31, 39, -23), 0); AcUnit(m, new Vector3(33.5f, 39, -23), 0); AcUnit(m, new Vector3(39, 39, -12), 90);
            m.StreetLamp(new Vector3(40f, 39, -20f), 270f, new Color(1f, 0.85f, 0.6f), 3.2f, 14f, 1.8f);
            Antenna(m, new Vector3(40.5f, 39, -24.5f), 7f);

            // Loose props on the roofs (dynamic, seeded like every map so all players get the same ones).
            Spawn("crate", B1.Top(-13f, -12f) + Vector3.up * 0.5f); Spawn("crate", B1.Top(-13.9f, -12.1f) + Vector3.up * 1.4f);
            Spawn("small_crate", B1.Top(-12f, -13.2f) + Vector3.up * 0.4f); Spawn("pallet", B1.Top(-16f, -27f) + Vector3.up * 0.2f);
            Spawn("barrel", B2.Top(2f, -26f) + Vector3.up * 0.6f); Spawn("explosive_barrel", B2.Top(3f, -26.5f) + Vector3.up * 0.6f);
            Spawn("crate", B3.Top(-27f, 10f) + Vector3.up * 0.5f); Spawn("plank", B3.Top(-22f, 8f) + Vector3.up * 0.3f, 30f);
            Spawn("propane", B4.Top(9f, 7f) + Vector3.up * 0.6f); Spawn("crate", B6.Top(36f, -10f) + Vector3.up * 0.5f);
            Spawn("explosive_barrel", B6.Top(30.5f, -10f) + Vector3.up * 0.6f); Spawn("mattress", B5.Top(0f, 13f) + Vector3.up * 0.3f);

            // ---- Street props far below.
            var paints = new[] { new Color(0.6f, 0.05f, 0.05f), new Color(0.1f, 0.2f, 0.5f), new Color(0.85f, 0.85f, 0.85f), new Color(0.12f, 0.12f, 0.12f), new Color(0.75f, 0.6f, 0.1f) };
            m.Car(new Vector3(-4f, 0, -22f), 0, paints[0]); m.Car(new Vector3(-3.5f, 0, -36f), 180, paints[2]);
            m.Car(new Vector3(22.5f, 0, -20f), 0, paints[1]); m.Car(new Vector3(-20f, 0, -4f), 90, paints[4]);
            m.Car(new Vector3(3f, 0, 0f), 90, paints[3]); m.Car(new Vector3(46f, 0, -2f), 180, paints[2]);
            m.Car(new Vector3(-36f, 0, 10f), 0, paints[1]);
            foreach (var p in new[] { new Vector3(-6, 0.15f, -2), new Vector3(22, 0.15f, -4), new Vector3(-34, 0.15f, -10), new Vector3(4, 0.15f, 30), new Vector3(46, 0.15f, -30) })
                m.StreetLamp(p, 45f, new Color(1f, 0.8f, 0.5f), 5f, 16f, 2f);
            Dumpster(m, new Vector3(-7f, 0.15f, 2.5f), 0); Dumpster(m, new Vector3(20.5f, 0.15f, -6f), 90);
            for (int i = 0; i < 5; i++) Spawn("cone", new Vector3(-2f + i * 0.9f, 0.1f, 26f));
            Spawn("mattress", new Vector3(23f, 0.4f, 0f)); Spawn("barrel", new Vector3(-8.2f, 0.7f, 4.5f));

            // ---- Skyline backdrop (no colliders, one mesh for the blocks, one for the lit windows).
            Skyline(m, lit);

            Mesh(m, "Lit Windows", lit, Mats.Glow(new Color(1f, 0.82f, 0.5f), 1.4f));
            Mesh(m, "Dark Windows", dark, Mats.Lit);

            // ---- Spawns & bounds
            m.PlayerSpawn = B1.Top(-19f, -24f) + Vector3.up * 0.1f;
            m.PlayerYaw = 20f;
            m.NpcSpawns.Add(B1.Top(-14f, -18f) + Vector3.up * 0.1f);
            m.NpcSpawns.Add(B2.Top(8f, -18f) + Vector3.up * 0.1f);
            m.NpcSpawns.Add(B3.Top(-20f, 12f) + Vector3.up * 0.1f);
            m.NpcSpawns.Add(B4.Top(16f, 8f) + Vector3.up * 0.1f);
            m.NpcSpawns.Add(B6.Top(35f, -16f) + Vector3.up * 0.1f);
            m.NpcSpawns.Add(B5.Top(-2f, 15f) + Vector3.up * 0.1f);
            m.Boundaries(new Bounds(new Vector3(6f, 38f, -2f), new Vector3(96f, 80f, 80f)), 80f);
        }

        static void Spawn(string id, Vector3 p, float yaw = 0f) => PropFactory.Spawn(id, p, Quaternion.Euler(0, yaw, 0));

        /// <summary>A board whose top surface runs from <paramref name="a"/> to <paramref name="b"/>, overlapping both ends a little.</summary>
        static void Plank(MapBuilder m, Vector3 a, Vector3 b, float width, Material mat, SurfaceType surface, Color tint, bool rails)
        {
            var d = b - a;
            var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            const float thick = 0.2f;
            var center = (a + b) * 0.5f - rot * Vector3.up * (thick * 0.5f);
            m.Box(rails ? "Bridge" : "Plank", center, rot, new Vector3(width, thick, d.magnitude), mat, surface, tint, 0.5f);
            if (!rails) return;
            for (int s = -1; s <= 1; s += 2)
            {
                var side = rot * Vector3.right * (width * 0.5f - 0.05f) * s;
                m.Box("Bridge Rail", (a + b) * 0.5f + side + rot * Vector3.up * 1.0f, rot, new Vector3(0.07f, 0.07f, d.magnitude), Mats.LitMetal, SurfaceType.Metal, tint);
                m.Box("Bridge Rail", (a + b) * 0.5f + side + rot * Vector3.up * 0.5f, rot, new Vector3(0.05f, 0.05f, d.magnitude), Mats.LitMetal, SurfaceType.Metal, tint, 0.5f, false);
                int posts = Mathf.Max(2, Mathf.CeilToInt(d.magnitude / 2.5f) + 1);
                for (int i = 0; i < posts; i++)
                    m.Box("Bridge Post", Vector3.Lerp(a, b, i / (float)(posts - 1)) + side + Vector3.up * 0.5f, Quaternion.identity, new Vector3(0.07f, 1f, 0.07f), Mats.LitMetal, SurfaceType.Metal, tint, 0.5f, false);
            }
        }

        /// <summary>Low curb around a roof, broken where a gap point lies on that edge.</summary>
        static void Parapet(MapBuilder m, Roof r, List<Vector3> gaps, Material mat)
        {
            const float h = 0.35f, t = 0.3f, gapHalf = 1.9f;
            float y = r.Max.y;
            var tint = new Color(0.45f, 0.45f, 0.47f);
            // Edges along X (at z = min / max) and along Z (at x = min / max).
            for (int e = 0; e < 4; e++)
            {
                bool alongX = e < 2;
                float fixedC = alongX ? (e == 0 ? r.Min.z : r.Max.z) : (e == 2 ? r.Min.x : r.Max.x);
                float lo = alongX ? r.Min.x : r.Min.z, hi = alongX ? r.Max.x : r.Max.z;
                var cuts = new List<float>();
                foreach (var g in gaps)
                {
                    float gFixed = alongX ? g.z : g.x, gRun = alongX ? g.x : g.z;
                    if (Mathf.Abs(gFixed - fixedC) < 0.7f && gRun > lo - 0.5f && gRun < hi + 0.5f) cuts.Add(gRun);
                }
                cuts.Sort();
                float start = lo;
                void Seg(float s0, float s1)
                {
                    if (s1 - s0 < 0.2f) return;
                    float inset = (e == 0 || e == 2) ? t * 0.5f : -t * 0.5f;
                    Vector3 mn, mx;
                    if (alongX) { mn = new Vector3(s0, y, fixedC + inset - t * 0.5f); mx = new Vector3(s1, y + h, fixedC + inset + t * 0.5f); }
                    else { mn = new Vector3(fixedC + inset - t * 0.5f, y, s0); mx = new Vector3(fixedC + inset + t * 0.5f, y + h, s1); }
                    m.Block("Parapet", mn, mx, mat, SurfaceType.Concrete, tint, 0.5f);
                }
                foreach (var c in cuts) { Seg(start, c - gapHalf); start = Mathf.Max(start, c + gapHalf); }
                Seg(start, hi);
            }
        }

        static void AcUnit(MapBuilder m, Vector3 basePos, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var body = new Color(0.72f, 0.73f, 0.72f);
            m.Box("AC Unit", basePos + Vector3.up * 0.6f, rot, new Vector3(1.7f, 1.2f, 1.2f), Mats.LitMetal, SurfaceType.Metal, body);
            m.Box("AC Base", basePos + Vector3.up * 0.06f, rot, new Vector3(1.9f, 0.12f, 1.4f), Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f), 0.5f, false);
            m.Cylinder("AC Fan", basePos + Vector3.up * 1.2f, 0.45f, 0.06f, Mats.LitMetal, SurfaceType.Metal, new Color(0.12f, 0.12f, 0.13f), 16, false);
            for (int i = -2; i <= 2; i++)
                m.Box("AC Grill", basePos + rot * new Vector3(i * 0.28f, 0.6f, 0.61f), rot, new Vector3(0.05f, 0.9f, 0.03f), Mats.LitMetal, SurfaceType.Metal, new Color(0.35f, 0.36f, 0.37f), 0.5f, false);
            m.Box("AC Duct", basePos + rot * new Vector3(0, 0.25f, -1.1f), rot, new Vector3(0.5f, 0.4f, 1f), Mats.LitMetal, SurfaceType.Metal, body * 0.85f);
        }

        static void WaterTower(MapBuilder m, Vector3 basePos)
        {
            var leg = new Color(0.25f, 0.22f, 0.2f);
            for (int i = 0; i < 4; i++)
            {
                var o = new Vector3(i % 2 == 0 ? -1.5f : 1.5f, 0, i < 2 ? -1.5f : 1.5f);
                m.Cylinder("Tower Leg", basePos + o, 0.13f, 4f, Mats.LitMetal, SurfaceType.Metal, leg, 8);
            }
            m.Box("Tower Brace", basePos + new Vector3(0, 2f, -1.5f), Quaternion.Euler(0, 0, 53f), new Vector3(0.08f, 4.8f, 0.08f), Mats.LitMetal, SurfaceType.Metal, leg, 0.5f, false);
            m.Box("Tower Brace", basePos + new Vector3(0, 2f, 1.5f), Quaternion.Euler(0, 0, -53f), new Vector3(0.08f, 4.8f, 0.08f), Mats.LitMetal, SurfaceType.Metal, leg, 0.5f, false);
            m.Box("Tower Platform", basePos + Vector3.up * 4.05f, Quaternion.identity, new Vector3(4f, 0.1f, 4f), MapBuilder.Tex(TextureFactory.Wood), SurfaceType.Wood, new Color(0.5f, 0.38f, 0.28f));
            m.Cylinder("Water Tank", basePos + Vector3.up * 4.1f, 2f, 4.2f, MapBuilder.Tex(TextureFactory.Wood), SurfaceType.Wood, new Color(0.62f, 0.45f, 0.32f), 18);
            for (int i = 0; i < 3; i++)
                m.Cylinder("Tank Hoop", basePos + Vector3.up * (4.6f + i * 1.4f), 2.04f, 0.08f, Mats.LitMetal, SurfaceType.Metal, new Color(0.2f, 0.2f, 0.22f), 18, false);
            // Conical roof.
            var go = new GameObject("Tank Roof");
            go.transform.SetParent(m.Root, false);
            go.transform.position = basePos + Vector3.up * 8.3f;
            var mb = new MeshBuilder { Color = new Color(0.3f, 0.25f, 0.22f), Gloss = 0.2f };
            mb.Cylinder(Vector3.zero, Vector3.up * 1.4f, 2.25f, 0.05f, 18);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("TankRoof");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
        }

        static void Antenna(MapBuilder m, Vector3 basePos, float height)
        {
            var c = new Color(0.4f, 0.4f, 0.42f);
            m.Cylinder("Antenna", basePos, 0.08f, height, Mats.LitMetal, SurfaceType.Metal, c, 6);
            for (float y = 1.5f; y < height - 0.8f; y += 1.8f)
                m.Box("Antenna Bar", basePos + Vector3.up * y, Quaternion.Euler(0, y * 37f, 0), new Vector3(1.2f - y / height * 0.8f, 0.04f, 0.04f), Mats.LitMetal, SurfaceType.Metal, c, 0.5f, false);
            m.Box("Antenna Beacon", basePos + Vector3.up * (height + 0.1f), Quaternion.identity, new Vector3(0.22f, 0.22f, 0.22f), Mats.Glow(new Color(1f, 0.1f, 0.08f), 3f), SurfaceType.Glass, Color.white, 1, false);
        }

        static void Dumpster(MapBuilder m, Vector3 basePos, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            m.Box("Dumpster", basePos + Vector3.up * 0.65f, rot, new Vector3(1.9f, 1.3f, 1.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.15f, 0.35f, 0.2f));
            m.Box("Dumpster Lid", basePos + rot * new Vector3(0, 1.33f, 0), rot * Quaternion.Euler(-6f, 0, 0), new Vector3(1.95f, 0.06f, 1.15f), Mats.LitMetal, SurfaceType.Metal, new Color(0.1f, 0.12f, 0.1f), 0.5f, false);
        }

        /// <summary>Window quads on all four faces of a box. <paramref name="litChance"/> of them glow.</summary>
        static void Windows(MeshBuilder lit, MeshBuilder dark, Vector3 min, Vector3 max, float w, float h, float stepX, float stepY, float litChance, bool withDark)
        {
            for (int face = 0; face < 4; face++)
            {
                bool alongX = face < 2;
                float lo = alongX ? min.x : min.z, hi = alongX ? max.x : max.z;
                float fixedC = face == 0 ? min.z - 0.04f : face == 1 ? max.z + 0.04f : face == 2 ? min.x - 0.04f : max.x + 0.04f;
                var normal = face == 0 ? Vector3.back : face == 1 ? Vector3.forward : face == 2 ? Vector3.left : Vector3.right;
                int cols = Mathf.FloorToInt((hi - lo - 1f) / stepX);
                float startRun = lo + (hi - lo - (cols - 1) * stepX) * 0.5f;
                for (float y = min.y + 3f; y < max.y - 0.5f; y += stepY)
                    for (int i = 0; i < cols; i++)
                    {
                        float run = startRun + i * stepX;
                        bool on = Random.value < litChance;
                        if (!on && !withDark) continue;
                        var c = alongX ? new Vector3(run, y, fixedC) : new Vector3(fixedC, y, run);
                        var mb = on ? lit : dark;
                        if (on) mb.SetColor(Color.Lerp(new Color(1f, 0.85f, 0.6f), new Color(0.7f, 0.85f, 1f), Random.value * Random.value));
                        mb.Face(c, normal, Vector3.up, w * 0.5f, h * 0.5f);
                    }
            }
            lit.SetColor(Color.white);
        }

        static void Skyline(MapBuilder m, MeshBuilder lit)
        {
            var blocks = new MeshBuilder { Color = new Color(0.07f, 0.075f, 0.09f), Gloss = 0.1f };
            var center = new Vector3(6f, 0, -2f);
            int n = 70;
            for (int i = 0; i < n; i++)
            {
                float ang = (i + Random.value * 0.6f) / n * Mathf.PI * 2f;
                float dist = Random.Range(75f, 165f);
                var p = center + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                float wdt = Random.Range(12f, 28f), dep = Random.Range(12f, 28f), hgt = Random.Range(30f, 140f);
                var size = new Vector3(wdt, hgt, dep);
                blocks.SetColor(new Color(0.06f, 0.065f, 0.08f) * Random.Range(0.8f, 1.4f));
                blocks.Box(p + Vector3.up * (hgt * 0.5f - 1f), size, 0.1f);
                if (Random.value < 0.35f) blocks.Box(p + Vector3.up * (hgt + 2f), new Vector3(wdt * 0.5f, 6f, dep * 0.5f), 0.1f); // setback top
                Windows(lit, null, p - new Vector3(wdt * 0.5f, 0, dep * 0.5f), p + new Vector3(wdt * 0.5f, hgt - 2f, dep * 0.5f), 2f, 2.4f, 5f, 6f, 0.28f, false);
                if (hgt > 100f) lit.Box(p + Vector3.up * (hgt + 0.3f), new Vector3(0.6f, 0.6f, 0.6f)); // roof beacon
            }
            Mesh(m, "Skyline", blocks, Mats.Lit);
        }

        static void Mesh(MapBuilder m, string name, MeshBuilder mb, Material mat)
        {
            if (mb.VertexCount == 0) return;
            var go = new GameObject(name);
            go.transform.SetParent(m.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ thumbnail

        public static void PaintThumb(PixelCanvas c, int W, int H)
        {
            var rng = new System.Random(77);
            for (int i = 0; i < 60; i++) c.Circle(rng.Next(W), 120 + rng.Next(96), 1, new Color(1, 1, 1, 0.6f), 1);
            c.Circle(320, 180, 14, new Color(0.95f, 0.95f, 1f), 3);
            // Distant skyline.
            for (int x = 0; x < W;)
            {
                int bw = 14 + rng.Next(22), bh = 60 + rng.Next(90);
                c.Rect(x, 0, bw, bh, new Color(0.08f, 0.08f, 0.13f));
                for (int wy = 8; wy < bh - 6; wy += 9) for (int wx = 3; wx < bw - 4; wx += 6) if (rng.NextDouble() < 0.25) c.Rect(x + wx, wy, 3, 4, new Color(1f, 0.82f, 0.5f, 0.7f));
                x += bw + 2;
            }
            // Foreground roofs at different heights + a plank between them.
            var roof = new Color(0.03f, 0.03f, 0.05f);
            c.Rect(0, 0, 130, 70, roof); c.Rect(170, 0, 110, 95, roof); c.Rect(310, 0, 74, 58, roof);
            c.Line(126, 70, 172, 95, 4, new Color(0.45f, 0.35f, 0.25f));
            // Water tower + antenna + helipad lights.
            c.Rect(36, 70, 3, 18, roof); c.Rect(62, 70, 3, 18, roof); c.Rect(30, 88, 40, 26, roof);
            c.Polygon(new[] { new Vector2(28, 114), new Vector2(72, 114), new Vector2(50, 126) }, roof);
            c.Rect(250, 95, 2, 50, roof); c.Circle(251, 146, 3, new Color(1f, 0.15f, 0.1f), 2);
            for (int i = 0; i < 6; i++) c.Circle(185 + i * 16, 97, 2, new Color(1f, 0.6f, 0.2f), 2);
            // Falling Goredoll silhouette.
            var sil = new Color(0.02f, 0.02f, 0.03f);
            c.Circle(150, 40, 5, sil, 1); c.Line(150, 35, 154, 18, 4, sil); c.Line(152, 30, 142, 24, 3, sil); c.Line(152, 30, 162, 36, 3, sil);
            c.Line(154, 18, 146, 6, 3, sil); c.Line(154, 18, 162, 8, 3, sil);
        }
    }
}
