using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Content Update maps: Hospital, Subway, Slaughterhouse, Construction Site and Prison. Each has its own
    /// deadly machinery: a lift and morgue, moving trains and a live third rail, hook rails and grinders, a crane
    /// and falling girders, an electric chair and a riot button.
    /// </summary>
    public static class ContentMaps
    {
        public static IEnumerable<MapDef> Defs()
        {
            yield return new MapDef
            {
                Id = "hospital", Name = "St. Mercy Hospital", Size = "Medium - indoor, 2 floors", Glyph = "heart", DefaultTime = TimeOfDay.Night, Indoor = true, Ambience = "amb_hum",
                Description = "A hospital after hours: wards, an operating theatre, a morgue full of drawers and a working lift between the two floors.",
                Features = new[] { "2 floors + lift", "Operating theatre", "Morgue drawers", "Wards", "Flickering lights" },
                ThemeTop = new Color(0.75f, 0.85f, 0.85f), ThemeBottom = new Color(0.2f, 0.35f, 0.38f), Accent = new Color(0.4f, 0.9f, 0.8f), Build = Hospital,
            };
            yield return new MapDef
            {
                Id = "subway", Name = "Subway Station", Size = "Medium - underground", Glyph = "flag", DefaultTime = TimeOfDay.Night, Indoor = true, Ambience = "amb_hum",
                Description = "An underground station. Trains come through every half minute and don't stop for anyone. The third rail is live.",
                Features = new[] { "Moving trains", "Live third rail", "Tunnels", "Two platforms", "Stairs to the street" },
                ThemeTop = new Color(0.15f, 0.15f, 0.18f), ThemeBottom = new Color(0.6f, 0.55f, 0.2f), Accent = new Color(1f, 0.85f, 0.2f), Build = Subway,
            };
            yield return new MapDef
            {
                Id = "slaughterhouse", Name = "Slaughterhouse", Size = "Medium - indoor", Glyph = "cleaver", DefaultTime = TimeOfDay.Night, Indoor = true, Ambience = "amb_drone",
                Description = "A meat processing plant: a hook rail, conveyor belts into the grinder and the shredder, and a walk-in freezer.",
                Features = new[] { "Meat hooks", "Conveyors", "Grinder + shredder", "Freezer", "Already bloody" },
                ThemeTop = new Color(0.35f, 0.05f, 0.05f), ThemeBottom = new Color(0.75f, 0.75f, 0.72f), Accent = new Color(1f, 0.25f, 0.2f), Build = Slaughterhouse,
            };
            yield return new MapDef
            {
                Id = "construction", Name = "Construction Site", Size = "Large - outdoor", Glyph = "wrench", DefaultTime = TimeOfDay.Day, Ambience = "amb_city",
                Description = "A half-built skyscraper: a five-floor steel frame, a crane swinging a girder, a cement mixer and beams that fall from the top floor.",
                Features = new[] { "Climbable steel frame", "Crane + swinging girder", "Cement mixer", "Falling beams", "Scaffolding" },
                ThemeTop = new Color(0.45f, 0.65f, 0.9f), ThemeBottom = new Color(0.85f, 0.65f, 0.2f), Accent = new Color(1f, 0.75f, 0.1f), Build = Construction,
            };
            yield return new MapDef
            {
                Id = "prison", Name = "Blackgate Prison", Size = "Large - indoor + yard", Glyph = "lock", DefaultTime = TimeOfDay.Night, Ambience = "amb_hum",
                Description = "A maximum security prison: a two-tier cell block, the yard and guard tower, the cafeteria, the electric chair, and a RIOT button.",
                Features = new[] { "Cell block", "Yard + guard tower", "Electric chair", "Riot mode", "Cafeteria" },
                ThemeTop = new Color(0.12f, 0.12f, 0.15f), ThemeBottom = new Color(0.95f, 0.45f, 0.08f), Accent = new Color(1f, 0.5f, 0.1f), Build = Prison,
            };
        }

        static Material T(Texture2D t) => MapBuilder.Tex(t);
        static Quaternion Y(float y) => Quaternion.Euler(0, y, 0);

        /// <summary>A box room. doors: (side 0 -X, 1 +X, 2 -Z, 3 +Z; centre along that wall; width), 2.8 m tall openings.</summary>
        static void Room(MapBuilder m, Vector3 min, Vector3 max, Texture2D wall, Color tint, Texture2D floor, Color floorTint, bool ceiling = true, params (int side, float at, float width)[] doors)
        {
            m.Block("Floor", new Vector3(min.x, min.y - 0.3f, min.z), new Vector3(max.x, min.y, max.z), T(floor), SurfaceType.Concrete, floorTint, 0.5f);
            if (ceiling) m.Block("Ceiling", new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y + 0.3f, max.z), T(TextureFactory.Ceiling), SurfaceType.Concrete, Color.white, 0.5f);
            for (int side = 0; side < 4; side++)
            {
                bool alongZ = side < 2;
                float fixedCoord = side == 0 ? min.x - 0.3f : side == 1 ? max.x : side == 2 ? min.z - 0.3f : max.z;
                float a0 = alongZ ? min.z : min.x, a1 = alongZ ? max.z : max.x;
                var gaps = new List<(float, float)>();
                foreach (var d in doors) if (d.side == side) gaps.Add((d.at - d.width / 2f, d.at + d.width / 2f));
                gaps.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                float cur = a0;
                foreach (var g in gaps)
                {
                    if (g.Item1 > cur) WallPiece(m, alongZ, fixedCoord, cur, g.Item1, min.y, max.y, wall, tint);
                    WallPiece(m, alongZ, fixedCoord, g.Item1, g.Item2, min.y + 2.8f, max.y, wall, tint);
                    cur = Mathf.Max(cur, g.Item2);
                }
                if (cur < a1) WallPiece(m, alongZ, fixedCoord, cur, a1, min.y, max.y, wall, tint);
            }
        }

        static void WallPiece(MapBuilder m, bool alongZ, float fixedCoord, float a, float b, float y0, float y1, Texture2D wall, Color tint)
        {
            if (b - a < 0.01f || y1 - y0 < 0.01f) return;
            var lo = alongZ ? new Vector3(fixedCoord, y0, a) : new Vector3(a, y0, fixedCoord);
            var hi = alongZ ? new Vector3(fixedCoord + 0.3f, y1, b) : new Vector3(b, y1, fixedCoord + 0.3f);
            m.Block("Wall", lo, hi, T(wall), SurfaceType.Concrete, tint, 0.4f);
        }

        static void Bed(MapBuilder m, Vector3 p, float yaw)
        {
            var r = Y(yaw);
            m.Box("Bed Frame", p + Vector3.up * 0.45f, r, new Vector3(1f, 0.1f, 2.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.75f, 0.76f, 0.8f));
            m.Box("Mattress", p + Vector3.up * 0.58f, r, new Vector3(0.95f, 0.16f, 2f), T(TextureFactory.White), SurfaceType.Carpet, new Color(0.85f, 0.92f, 0.95f));
            m.Box("Pillow", p + Vector3.up * 0.7f + r * Vector3.back * 0.8f, r, new Vector3(0.7f, 0.1f, 0.35f), T(TextureFactory.White), SurfaceType.Carpet, Color.white, 1f, false);
            for (int i = 0; i < 4; i++) m.Box("Bed Leg", p + r * new Vector3(i < 2 ? 0.45f : -0.45f, 0.2f, i % 2 == 0 ? 0.95f : -0.95f), r, new Vector3(0.05f, 0.4f, 0.05f), Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.6f, 0.62f), 1f, false);
        }

        // ====================================================================== hospital

        static void Hospital(MapBuilder m)
        {
            var tile = TextureFactory.Tiles; var wall = TextureFactory.Plaster;
            Color wallC = new Color(0.82f, 0.9f, 0.88f), floorC = new Color(0.85f, 0.88f, 0.86f);
            // Ground floor: 40 x 30, second floor above (y = 4.5).
            Room(m, new Vector3(-20, 0, -15), new Vector3(20, 9.3f, 15), wall, wallC, tile, floorC, true);
            m.Block("Floor 2", new Vector3(-20, 4.2f, -15), new Vector3(14, 4.5f, 15), T(tile), SurfaceType.Concrete, floorC, 0.5f);
            // Corridor walls (ground): wards to the north, theatre + morgue to the south
            for (int i = 0; i < 4; i++)
            {
                float x = -18 + i * 9;
                m.Block("Ward Wall", new Vector3(x - 0.15f, 0, 3), new Vector3(x + 0.15f, 4.2f, 15), T(wall), SurfaceType.Concrete, wallC, 0.4f);
                m.Block("Ward Front", new Vector3(x, 0, 3), new Vector3(x + 5.5f, 4.2f, 3.3f), T(wall), SurfaceType.Concrete, wallC, 0.4f);
                Bed(m, new Vector3(x + 3, 0, 9), 0f);
                Bed(m, new Vector3(x + 6.5f, 0, 9), 0f);
                m.Box("Drip Stand", new Vector3(x + 4.6f, 0.9f, 10f), Quaternion.identity, new Vector3(0.05f, 1.8f, 0.05f), Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.7f, 0.72f));
                m.CeilingLight(new Vector3(x + 4.5f, 4.0f, 9), new Color(0.9f, 1f, 0.95f), 10f, 1.4f);
            }
            // Operating theatre (south west)
            m.Block("Theatre Wall", new Vector3(-20, 0, -3.3f), new Vector3(-6, 4.2f, -3f), T(wall), SurfaceType.Concrete, new Color(0.6f, 0.8f, 0.75f), 0.4f);
            m.Block("Theatre Wall", new Vector3(-6, 0, -15), new Vector3(-5.7f, 4.2f, -6f), T(wall), SurfaceType.Concrete, new Color(0.6f, 0.8f, 0.75f), 0.4f);
            m.Box("Operating Table", new Vector3(-13, 0.9f, -9), Quaternion.identity, new Vector3(0.8f, 0.12f, 2.2f), Mats.LitMetal, SurfaceType.Metal, new Color(0.8f, 0.82f, 0.85f));
            m.Box("Table Post", new Vector3(-13, 0.45f, -9), Quaternion.identity, new Vector3(0.25f, 0.9f, 0.25f), Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.6f, 0.62f));
            m.Box("Surgical Lamp", new Vector3(-13, 3.4f, -9), Quaternion.identity, new Vector3(1.1f, 0.15f, 1.1f), Mats.Glow(new Color(1f, 1f, 0.95f), 2f), SurfaceType.Glass, Color.white, 1f, false);
            m.CeilingLight(new Vector3(-13, 3.2f, -9), new Color(1f, 1f, 0.97f), 9f, 2.5f);
            m.Box("Tool Tray", new Vector3(-11.4f, 1.0f, -9), Quaternion.identity, new Vector3(0.6f, 0.04f, 0.4f), Mats.LitMetal, SurfaceType.Metal, new Color(0.75f, 0.76f, 0.8f));
            for (int k = 0; k < 4; k++) m.Box("Instrument", new Vector3(-11.6f + k * 0.12f, 1.03f, -9f), Quaternion.Euler(0, 10 * k, 0), new Vector3(0.015f, 0.01f, 0.22f), Mats.LitMetal, SurfaceType.Metal, new Color(0.85f, 0.86f, 0.9f), 1f, false);
            Decals.Blood(new Vector3(-13, 0.02f, -9), Vector3.up, 1.4f, 0, null);
            // Morgue (south east): wall of drawers + a slab
            m.Block("Morgue Wall", new Vector3(6, 0, -3.3f), new Vector3(20, 4.2f, -3f), T(TextureFactory.Metal), SurfaceType.Metal, new Color(0.55f, 0.6f, 0.62f), 0.4f);
            for (int r = 0; r < 3; r++) for (int c = 0; c < 6; c++)
                m.Box("Drawer", new Vector3(8 + c * 2f, 0.6f + r * 1.1f, -14.6f), Quaternion.identity, new Vector3(1.8f, 0.95f, 0.15f), Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.72f, 0.75f));
            m.Box("Morgue Slab", new Vector3(13, 0.85f, -9), Quaternion.identity, new Vector3(1f, 0.12f, 2.2f), Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.62f, 0.65f));
            m.CeilingLight(new Vector3(13, 4f, -9), new Color(0.7f, 0.85f, 1f), 12f, 1.1f);
            // Stairs and a lift to floor 2
            m.Stairs("Stairs", new Vector3(15.5f, 0, 10f), 180f, 2.5f, 15, 0.3f, 0.45f, T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.7f, 0.7f, 0.7f));
            Lift.Create(m.Root, new Vector3(17.8f, 0f, -0.5f), 4.5f);
            m.Box("Lift Shaft", new Vector3(19.4f, 4.6f, -0.5f), Quaternion.identity, new Vector3(0.2f, 9.2f, 3f), T(TextureFactory.Metal), SurfaceType.Metal, new Color(0.5f, 0.5f, 0.55f));
            // Floor 2: open ward
            for (int i = 0; i < 6; i++) Bed(m, new Vector3(-16 + i * 5, 4.5f, -10), 0f);
            for (int i = 0; i < 6; i++) Bed(m, new Vector3(-16 + i * 5, 4.5f, 10), 180f);
            for (int i = 0; i < 3; i++) m.CeilingLight(new Vector3(-12 + i * 10, 9f, 0), new Color(0.9f, 1f, 0.95f), 16f, 1.3f);
            for (int i = 0; i < 4; i++) m.CeilingLight(new Vector3(-15 + i * 10, 4f, -1), new Color(0.9f, 1f, 0.95f), 12f, 1.2f);
            PropFactory.Spawn("chair", new Vector3(-4, 0.4f, 0), Quaternion.identity);
            PropFactory.Spawn("table", new Vector3(-2, 0.4f, -1), Quaternion.identity);
            m.PlayerSpawn = new Vector3(0, 0.1f, -1f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-13, 0.1f, 8), new Vector3(-4, 0.1f, 8), new Vector3(5, 0.1f, 8), new Vector3(-13, 0.1f, -6), new Vector3(13, 0.1f, -6), new Vector3(-6, 4.6f, 0), new Vector3(4, 4.6f, 0) });
            m.Playable = new Bounds(new Vector3(0, 4.5f, 0), new Vector3(40, 9, 30));
        }

        // ====================================================================== subway

        static void Subway(MapBuilder m)
        {
            var tile = TextureFactory.Tiles;
            // Track pit (x along the track), platforms either side, tunnels at both ends.
            m.Block("Track Bed", new Vector3(-90, -1.6f, -3), new Vector3(90, -1.3f, 3), T(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.35f, 0.3f, 0.27f), 0.3f);
            for (float x = -88; x < 90; x += 1.2f) m.Box("Sleeper", new Vector3(x, -1.25f, 0), Quaternion.identity, new Vector3(0.25f, 0.1f, 2.6f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.35f, 0.25f, 0.18f), 1f, false);
            m.Box("Rail", new Vector3(0, -1.12f, -0.72f), Quaternion.identity, new Vector3(180f, 0.12f, 0.08f), Mats.LitMetal, SurfaceType.Metal, new Color(0.55f, 0.55f, 0.58f), 1f, false);
            m.Box("Rail", new Vector3(0, -1.12f, 0.72f), Quaternion.identity, new Vector3(180f, 0.12f, 0.08f), Mats.LitMetal, SurfaceType.Metal, new Color(0.55f, 0.55f, 0.58f), 1f, false);
            m.Box("Third Rail", new Vector3(0, -1.1f, 1.8f), Quaternion.identity, new Vector3(180f, 0.15f, 0.12f), Mats.Glow(new Color(1f, 0.85f, 0.2f), 0.4f), SurfaceType.Metal, Color.white, 1f, false);
            ThirdRail.Create(m.Root, new Vector3(0, -1f, 1.8f), new Vector3(180f, 0.5f, 0.6f));
            for (int s = -1; s <= 1; s += 2)
            {
                m.Block("Platform", new Vector3(-40, -1.6f, s > 0 ? 3 : -11), new Vector3(40, 0, s > 0 ? 11 : -3), T(tile), SurfaceType.Concrete, new Color(0.75f, 0.73f, 0.68f), 0.4f);
                m.Box("Yellow Line", new Vector3(0, 0.01f, s * 3.4f), Quaternion.identity, new Vector3(80f, 0.01f, 0.25f), Mats.Lit, SurfaceType.Concrete, new Color(0.95f, 0.8f, 0.1f), 1f, false);
                m.Block("Wall", new Vector3(-40, -1.6f, s > 0 ? 11 : -11.3f), new Vector3(40, 6, s > 0 ? 11.3f : -11), T(tile), SurfaceType.Concrete, new Color(0.85f, 0.85f, 0.8f), 0.3f);
                for (int i = 0; i < 6; i++)
                {
                    m.Box("Bench", new Vector3(-30 + i * 12, 0.25f, s * 9.5f), Quaternion.identity, new Vector3(2.4f, 0.5f, 0.5f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.45f, 0.3f, 0.18f));
                    m.Box("Pillar", new Vector3(-34 + i * 13.5f, 3f, s * 6f), Quaternion.identity, new Vector3(0.8f, 6f, 0.8f), T(tile), SurfaceType.Concrete, new Color(0.7f, 0.25f, 0.2f));
                    m.CeilingLight(new Vector3(-30 + i * 12, 5.7f, s * 7f), new Color(1f, 0.95f, 0.8f), 12f, 1.3f);
                }
                m.Box("Station Sign", new Vector3(0, 3f, s * 10.9f), Quaternion.identity, new Vector3(8f, 1f, 0.1f), Mats.Glow(new Color(0.2f, 0.5f, 1f), 1.5f), SurfaceType.Glass, Color.white, 1f, false);
            }
            m.Block("Ceiling", new Vector3(-40, 6, -11.3f), new Vector3(40, 6.4f, 11.3f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.5f, 0.5f, 0.5f));
            // Tunnels
            for (int e = -1; e <= 1; e += 2)
            {
                float x0 = e < 0 ? -90 : 40, x1 = e < 0 ? -40 : 90;
                m.Block("Tunnel Wall", new Vector3(x0, -1.6f, -3.3f), new Vector3(x1, 3.4f, -3f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.35f, 0.35f, 0.36f));
                m.Block("Tunnel Wall", new Vector3(x0, -1.6f, 3f), new Vector3(x1, 3.4f, 3.3f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.35f, 0.35f, 0.36f));
                m.Block("Tunnel Roof", new Vector3(x0, 3.4f, -3.3f), new Vector3(x1, 3.8f, 3.3f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.3f, 0.3f, 0.32f));
                for (float x = x0 + 6; x < x1; x += 12) m.CeilingLight(new Vector3(x, 3.2f, 2.6f), new Color(1f, 0.6f, 0.3f), 8f, 0.9f, new Vector3(0.3f, 0.1f, 0.3f));
                m.Block("Tunnel End", new Vector3(e < 0 ? -91 : 90, -1.6f, -3.3f), new Vector3(e < 0 ? -90 : 91, 3.8f, 3.3f), T(TextureFactory.Concrete), SurfaceType.Concrete, Color.black);
            }
            // Stairs up to a street exit
            m.Stairs("Exit Stairs", new Vector3(-36, 0, 8.5f), 90f, 3f, 18, 0.33f, 0.5f, T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.65f, 0.65f, 0.62f));
            Train.Create(m.Root, -95f, 95f, 30f);
            m.PlayerSpawn = new Vector3(0, 0.1f, -7f);
            m.PlayerYaw = 0f;
            m.NpcSpawns.AddRange(new[] { new Vector3(-20, 0.1f, -6), new Vector3(10, 0.1f, -7), new Vector3(25, 0.1f, -5), new Vector3(-15, 0.1f, 6), new Vector3(15, 0.1f, 7), new Vector3(30, 0.1f, 5) });
            m.Playable = new Bounds(new Vector3(0, 2f, 0), new Vector3(80, 8, 22));
            m.KillY = -20f;
        }

        // ====================================================================== slaughterhouse

        static void Slaughterhouse(MapBuilder m)
        {
            Room(m, new Vector3(-24, 0, -16), new Vector3(24, 7, 16), TextureFactory.Tiles, new Color(0.85f, 0.85f, 0.82f), TextureFactory.Tiles, new Color(0.7f, 0.68f, 0.65f));
            // Hook rail across the hall
            m.Box("Rail", new Vector3(0, 5.2f, 6), Quaternion.identity, new Vector3(40f, 0.2f, 0.2f), Mats.LitMetal, SurfaceType.Metal, new Color(0.5f, 0.5f, 0.52f), 1f, false);
            for (int i = 0; i < 8; i++) MeatHook.Create(new Vector3(-17 + i * 5, 1.6f, 6), Quaternion.identity);
            // Processing line: conveyor into the chipper, second conveyor into the shredder
            Conveyor.Create(new Vector3(-12, 0, -6), Y(90));
            Conveyor.Create(new Vector3(-7, 0, -6), Y(90));
            WoodChipper.Create(new Vector3(-2.2f, 0, -6), Y(-90));
            Conveyor.Create(new Vector3(6, 0, -10), Y(90));
            Shredder.Create(new Vector3(11, 0, -10), Quaternion.identity);
            Piston.Create(new Vector3(14, 0, 2), Quaternion.identity);
            // Butcher tables and carcasses
            for (int i = 0; i < 4; i++)
            {
                m.Box("Butcher Table", new Vector3(-18 + i * 4, 0.9f, -13), Quaternion.identity, new Vector3(1.6f, 0.1f, 1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.72f, 0.75f));
                m.Box("Table Legs", new Vector3(-18 + i * 4, 0.45f, -13), Quaternion.identity, new Vector3(1.4f, 0.9f, 0.08f), Mats.LitMetal, SurfaceType.Metal, new Color(0.5f, 0.5f, 0.52f), 1f, false);
                Decals.Blood(new Vector3(-18 + i * 4, 0.02f, -12), Vector3.up, 1.2f, 0, null);
            }
            for (int i = 0; i < 6; i++) Decals.Blood(new Vector3(-15 + i * 6, 0.02f, 6 + (i % 2)), Vector3.up, 1.5f, 0, null);
            // Freezer (north east): cold blue, fog, hanging carcass shapes
            m.Block("Freezer Wall", new Vector3(10, 0, 9), new Vector3(10.4f, 7, 16), T(TextureFactory.Metal), SurfaceType.Metal, new Color(0.7f, 0.8f, 0.9f));
            m.Block("Freezer Wall", new Vector3(10, 0, 9), new Vector3(16, 7, 9.4f), T(TextureFactory.Metal), SurfaceType.Metal, new Color(0.7f, 0.8f, 0.9f));
            m.Block("Freezer Wall", new Vector3(19, 0, 9), new Vector3(24, 7, 9.4f), T(TextureFactory.Metal), SurfaceType.Metal, new Color(0.7f, 0.8f, 0.9f));
            m.CeilingLight(new Vector3(17, 6.5f, 12.5f), new Color(0.5f, 0.7f, 1f), 12f, 1.6f);
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(12 + (i % 3) * 4, 3.6f, 11 + (i / 3) * 3);
                m.Box("Carcass", p, Quaternion.Euler(0, i * 30, 0), new Vector3(0.7f, 1.6f, 0.4f), Mats.Lit, SurfaceType.Flesh, new Color(0.65f, 0.15f, 0.12f), 1f, false);
                m.Box("Chain", p + Vector3.up * 1.4f, Quaternion.identity, new Vector3(0.04f, 1.2f, 0.04f), Mats.LitMetal, SurfaceType.Metal, new Color(0.5f, 0.5f, 0.52f), 1f, false);
            }
            for (int i = 0; i < 4; i++) m.CeilingLight(new Vector3(-16 + i * 10, 6.6f, -4), new Color(1f, 0.92f, 0.8f), 14f, 1.4f);
            for (int i = 0; i < 4; i++) m.CeilingLight(new Vector3(-16 + i * 9, 6.6f, 8), new Color(1f, 0.6f, 0.5f), 12f, 1.1f);
            m.PlayerSpawn = new Vector3(-20, 0.1f, 0);
            m.PlayerYaw = 90f;
            m.NpcSpawns.AddRange(new[] { new Vector3(-10, 0.1f, 2), new Vector3(0, 0.1f, 2), new Vector3(8, 0.1f, 3), new Vector3(-6, 0.1f, 10), new Vector3(4, 0.1f, 12), new Vector3(16, 0.1f, 12) });
            m.Playable = new Bounds(new Vector3(0, 3.5f, 0), new Vector3(48, 7, 32));
        }

        // ====================================================================== construction site

        static void Construction(MapBuilder m)
        {
            m.Block("Ground", new Vector3(-80, -1, -80), new Vector3(80, 0, 80), T(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.6f, 0.5f, 0.38f), 0.15f);
            m.Fence(new Vector3(-60, 0, -60), new Vector3(60, 0, -60), 2.4f);
            m.Fence(new Vector3(-60, 0, 60), new Vector3(60, 0, 60), 2.4f);
            m.Fence(new Vector3(-60, 0, -60), new Vector3(-60, 0, 60), 2.4f);
            m.Fence(new Vector3(60, 0, -60), new Vector3(60, 0, 60), 2.4f);
            // Steel frame: 5 floors, 4 m each, 24 x 18
            var steel = new Color(0.65f, 0.3f, 0.15f);
            for (int f = 0; f <= 5; f++)
            {
                float y = f * 4f;
                for (int i = 0; i <= 4; i++) for (int k = 0; k <= 3; k++)
                    if (f < 5) m.Box("Column", new Vector3(-12 + i * 6, y + 2f, -9 + k * 6), Quaternion.identity, new Vector3(0.35f, 4f, 0.35f), Mats.LitMetal, SurfaceType.Metal, steel);
                if (f == 0) continue;
                for (int i = 0; i <= 4; i++) m.Box("Beam", new Vector3(-12 + i * 6, y, 0), Quaternion.identity, new Vector3(0.3f, 0.4f, 18.3f), Mats.LitMetal, SurfaceType.Metal, steel);
                for (int k = 0; k <= 3; k++) m.Box("Beam", new Vector3(0, y, -9 + k * 6), Quaternion.identity, new Vector3(24.3f, 0.4f, 0.3f), Mats.LitMetal, SurfaceType.Metal, steel);
                // Partial floors (planks / concrete slabs)
                if (f < 5) for (int i = 0; i < 4; i++) for (int k = 0; k < 3; k++)
                    if ((i + k + f) % 3 != 0) m.Box("Slab", new Vector3(-9 + i * 6, y + 0.25f, -6 + k * 6), Quaternion.identity, new Vector3(5.7f, 0.12f, 5.7f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.65f, 0.65f, 0.62f));
            }
            // Stairs zig-zag up the east side
            for (int f = 0; f < 5; f++) m.Stairs("Site Stairs", new Vector3(14f + (f % 2) * 3f, f * 4f, f % 2 == 0 ? -8f : 2f), f % 2 == 0 ? 0f : 180f, 2.4f, 13, 0.31f, 0.75f, T(TextureFactory.Metal), SurfaceType.Metal, new Color(0.9f, 0.7f, 0.1f));
            // Scaffolding on the west side
            for (int f = 0; f < 3; f++) m.Box("Scaffold Deck", new Vector3(-14.5f, f * 4f + 2f, 0), Quaternion.identity, new Vector3(2.2f, 0.1f, 18f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.6f, 0.45f, 0.28f));
            for (int i = 0; i < 4; i++) m.Box("Scaffold Pole", new Vector3(-15.5f, 6f, -9 + i * 6), Quaternion.identity, new Vector3(0.08f, 12f, 0.08f), Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.7f, 0.72f));
            // Tower crane with a swinging girder
            m.Box("Crane Mast", new Vector3(30, 18f, 20), Quaternion.identity, new Vector3(1.4f, 36f, 1.4f), Mats.LitMetal, SurfaceType.Metal, new Color(0.95f, 0.75f, 0.1f));
            m.Box("Crane Jib", new Vector3(14, 36.5f, 20), Quaternion.identity, new Vector3(36f, 1f, 1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.95f, 0.75f, 0.1f));
            m.Box("Counterweight", new Vector3(36, 35.5f, 20), Quaternion.identity, new Vector3(4f, 2f, 2f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.5f, 0.5f, 0.5f));
            SwingingGirder.Create(m.Root, new Vector3(4, 36f, 20), 30f);
            // Cement mixer, beam dropper, site props
            CementMixer.Create(m.Root, new Vector3(-28, 0, -20));
            BeamDropper.Create(m.Root, new Vector3(0, 20.6f, 0), 12f);
            for (int i = 0; i < 6; i++) PropFactory.Spawn(i % 2 == 0 ? "concrete" : "cinder", new Vector3(-30 + i * 2, 0.5f, 15), Quaternion.Euler(0, i * 20, 0));
            for (int i = 0; i < 4; i++) PropFactory.Spawn("plank", new Vector3(-25, 0.3f + i * 0.12f, 28), Quaternion.identity);
            m.Box("Port-a-Potty", new Vector3(40, 1.2f, -30), Quaternion.identity, new Vector3(1.2f, 2.4f, 1.2f), Mats.Lit, SurfaceType.Wood, new Color(0.2f, 0.45f, 0.75f));
            m.Box("Site Office", new Vector3(-40, 1.5f, 35), Quaternion.identity, new Vector3(8f, 3f, 3f), T(TextureFactory.Metal), SurfaceType.Metal, new Color(0.85f, 0.85f, 0.82f));
            m.Car(new Vector3(35, 0, 35), 30f, new Color(0.95f, 0.75f, 0.1f));
            PropFactory.Spawn("veh_dozer", new Vector3(25, 0.6f, -30), Quaternion.Euler(0, -45, 0));
            m.PlayerSpawn = new Vector3(0, 0.1f, -40f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-6, 0.1f, -3), new Vector3(6, 0.1f, 3), new Vector3(-9, 4.5f, 0), new Vector3(3, 8.5f, -6), new Vector3(-3, 12.5f, 6), new Vector3(-25, 0.1f, -10), new Vector3(20, 0.1f, 10) });
            m.Playable = new Bounds(new Vector3(0, 10f, 0), new Vector3(120, 22, 120));
        }

        // ====================================================================== prison

        static void Prison(MapBuilder m)
        {
            var conc = TextureFactory.Concrete;
            Color grey = new Color(0.55f, 0.56f, 0.58f);
            // Cell block hall: 40 x 20, two tiers of cells along both long walls
            Room(m, new Vector3(-20, 0, -10), new Vector3(20, 9, 10), conc, grey, conc, new Color(0.5f, 0.5f, 0.5f), true, (2, 0f, 4f), (1, 0f, 3f), (0, 0f, 3f));
            for (int tier = 0; tier < 2; tier++)
            {
                float y = tier * 4.2f;
                if (tier == 1) { m.Block("Walkway", new Vector3(-20, 4f, 5.2f), new Vector3(20, 4.2f, 6.6f), T(TextureFactory.Metal), SurfaceType.Metal, grey); m.Block("Walkway", new Vector3(-20, 4f, -6.6f), new Vector3(20, 4.2f, -5.2f), T(TextureFactory.Metal), SurfaceType.Metal, grey); }
                for (int s = -1; s <= 1; s += 2)
                    for (int i = 0; i < 8; i++)
                    {
                        float x = -18 + i * 5;
                        float zWall = s * 10f, zFront = s * 5.2f;
                        m.Block("Cell Wall", new Vector3(x - 0.15f, y, Mathf.Min(zWall, zFront)), new Vector3(x + 0.15f, y + 4f, Mathf.Max(zWall, zFront)), T(conc), SurfaceType.Concrete, grey);
                        if (tier == 0 || true) m.Block("Cell Floor", new Vector3(x, y + (tier == 1 ? 4f : 0f), Mathf.Min(zWall, zFront)), new Vector3(x + 5f, y + (tier == 1 ? 4.2f : 0.01f), Mathf.Max(zWall, zFront)), T(conc), SurfaceType.Concrete, grey, 0.5f, tier == 1);
                        for (int b = 0; b < 9; b++) if (!(i % 2 == 0 && (b == 3 || b == 4))) m.Box("Bars", new Vector3(x + 0.5f + b * 0.5f, y + 2f + (tier == 1 ? 0.2f : 0f), zFront), Quaternion.identity, new Vector3(0.05f, 4f, 0.05f), Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f));
                        m.Box("Bunk", new Vector3(x + 2.5f, y + 0.6f + (tier == 1 ? 0.2f : 0f), s * 9f), Quaternion.identity, new Vector3(2f, 0.15f, 0.9f), Mats.LitMetal, SurfaceType.Metal, new Color(0.45f, 0.45f, 0.48f));
                        m.Box("Toilet", new Vector3(x + 4.3f, y + 0.25f + (tier == 1 ? 0.2f : 0f), s * 9.3f), Quaternion.identity, new Vector3(0.45f, 0.5f, 0.5f), Mats.LitMetal, SurfaceType.Metal, new Color(0.85f, 0.85f, 0.85f));
                    }
            }
            m.Stairs("Block Stairs", new Vector3(-19f, 0, -4f), 90f, 1.8f, 14, 0.3f, 0.5f, T(TextureFactory.Metal), SurfaceType.Metal, grey);
            for (int i = 0; i < 4; i++) m.CeilingLight(new Vector3(-15 + i * 10, 8.6f, 0), new Color(1f, 0.92f, 0.75f), 16f, 1.4f);
            // Cafeteria (east wing)
            Room(m, new Vector3(20.3f, 0, -10), new Vector3(36, 4.5f, 10), conc, new Color(0.65f, 0.62f, 0.55f), TextureFactory.Tiles, new Color(0.7f, 0.68f, 0.62f), true, (0, 0f, 3f));
            for (int i = 0; i < 3; i++) for (int k = 0; k < 2; k++) { var p = new Vector3(24 + i * 4, 0, -5 + k * 8); m.Box("Table", p + Vector3.up * 0.75f, Quaternion.identity, new Vector3(1.2f, 0.08f, 3f), Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.6f, 0.62f)); m.Box("Bench", p + new Vector3(0.9f, 0.45f, 0), Quaternion.identity, new Vector3(0.35f, 0.08f, 3f), Mats.LitMetal, SurfaceType.Metal, grey); m.Box("Bench", p + new Vector3(-0.9f, 0.45f, 0), Quaternion.identity, new Vector3(0.35f, 0.08f, 3f), Mats.LitMetal, SurfaceType.Metal, grey); }
            m.CeilingLight(new Vector3(28, 4.2f, 0), new Color(1f, 0.95f, 0.85f), 14f, 1.3f);
            // Execution room (west) with the electric chair
            Room(m, new Vector3(-32, 0, -6), new Vector3(-20.3f, 4.5f, 6), conc, new Color(0.35f, 0.38f, 0.4f), conc, new Color(0.4f, 0.4f, 0.4f), true, (1, 0f, 3f));
            ElectricChair.Create(new Vector3(-27, 0, 0), Y(90));
            m.CeilingLight(new Vector3(-27, 4.2f, 0), new Color(0.8f, 0.9f, 1f), 9f, 1.4f);
            // Yard (south) with a fence, a guard tower and the riot button in the guard booth
            m.Block("Yard", new Vector3(-30, -1, -50), new Vector3(30, 0, -10.3f), T(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.5f, 0.5f, 0.52f), 0.2f);
            m.Fence(new Vector3(-30, 0, -50), new Vector3(30, 0, -50), 4f);
            m.Fence(new Vector3(-30, 0, -50), new Vector3(-30, 0, -10.3f), 4f);
            m.Fence(new Vector3(30, 0, -50), new Vector3(30, 0, -10.3f), 4f);
            m.Box("Hoop", new Vector3(0, 3f, -48), Quaternion.identity, new Vector3(1.8f, 1.2f, 0.1f), Mats.Lit, SurfaceType.Wood, Color.white);
            for (int i = 0; i < 3; i++) m.Box("Weights Bench", new Vector3(-20 + i * 3, 0.4f, -40), Quaternion.identity, new Vector3(0.5f, 0.8f, 1.8f), Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.25f, 0.27f));
            m.Box("Tower Leg", new Vector3(26, 5f, -46), Quaternion.identity, new Vector3(0.4f, 10f, 0.4f), Mats.LitMetal, SurfaceType.Metal, grey);
            m.Box("Tower Cabin", new Vector3(26, 10.5f, -46), Quaternion.identity, new Vector3(4f, 2.5f, 4f), T(conc), SurfaceType.Concrete, grey);
            m.StreetLamp(new Vector3(26, 12f, -44), 0f, new Color(1f, 0.95f, 0.8f), 1f, 40f, 3f);
            m.Box("Guard Booth", new Vector3(-24, 1.4f, -14), Quaternion.identity, new Vector3(3f, 2.8f, 3f), T(conc), SurfaceType.Concrete, new Color(0.4f, 0.45f, 0.55f));
            PushButton.Create(m.Root, new Vector3(-22.4f, 1.2f, -14f), Quaternion.Euler(0, 90, 0), "START A RIOT", () => Riot.Start(new Vector3(0, 0.1f, 0)));
            for (int i = 0; i < 4; i++) m.StreetLamp(new Vector3(-24 + i * 16, 0, -30), 0f, new Color(1f, 0.85f, 0.6f), 7f, 22f, 2f);
            m.PlayerSpawn = new Vector3(-22, 0.1f, -18f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-13, 0.1f, 8), new Vector3(-3, 0.1f, -8), new Vector3(7, 0.1f, 8), new Vector3(0, 0.1f, -30), new Vector3(10, 0.1f, -35), new Vector3(28, 0.1f, 2) });
            m.Playable = new Bounds(new Vector3(0, 4.5f, -18), new Vector3(70, 9, 66));
        }
    }

    // ====================================================================== map machinery

    /// <summary>A lift platform going up and down between two floors.</summary>
    public sealed class Lift : MonoBehaviour
    {
        Rigidbody rb; Vector3 bottom; float height, t;
        public static void Create(Transform parent, Vector3 pos, float height)
        {
            var go = new GameObject("Lift");
            go.transform.SetParent(parent, false); go.transform.position = pos;
            var mb = new MeshBuilder { Color = new Color(0.6f, 0.62f, 0.66f), Gloss = 0.6f };
            mb.Box(new Vector3(0, 0.08f, 0), new Vector3(2.6f, 0.16f, 2.6f));
            mb.SetColor(new Color(0.9f, 0.75f, 0.1f)); mb.Box(new Vector3(0, 0.17f, 1.25f), new Vector3(2.6f, 0.01f, 0.1f));
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Lift");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.08f, 0); bc.size = new Vector3(2.6f, 0.16f, 2.6f);
            var l = go.AddComponent<Lift>();
            l.rb = go.AddComponent<Rigidbody>(); l.rb.isKinematic = true;
            l.bottom = pos; l.height = height;
        }
        void FixedUpdate()
        {
            t += Time.fixedDeltaTime;
            float c = Mathf.Repeat(t, 14f);
            float u = c < 4f ? 0f : c < 7f ? Mathf.SmoothStep(0f, 1f, (c - 4f) / 3f) : c < 11f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (c - 11f) / 3f);
            rb.MovePosition(bottom + Vector3.up * height * u);
        }
    }

    /// <summary>Subway train: runs through the station every interval at full speed. Anything on the tracks is hit.</summary>
    public sealed class Train : MonoBehaviour
    {
        float from, to, interval, t; Rigidbody rb; bool horned;
        public static void Create(Transform parent, float fromX, float toX, float interval)
        {
            var go = new GameObject("Train");
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(fromX, -1.1f, 0);
            var mb = new MeshBuilder { Color = new Color(0.75f, 0.76f, 0.8f), Gloss = 0.7f };
            for (int car = 0; car < 4; car++)
            {
                float x = -car * 16f;
                mb.SetColor(new Color(0.75f, 0.76f, 0.8f), 0.7f);
                mb.BevelBox(new Vector3(x - 7.5f, 2f, 0), new Vector3(15.4f, 3.2f, 2.9f), 0.2f);
                mb.SetColor(new Color(0.85f, 0.15f, 0.12f), 0.5f);
                mb.Box(new Vector3(x - 7.5f, 1.2f, 0), new Vector3(15.5f, 0.25f, 2.95f));
                mb.SetColor(new Color(0.1f, 0.15f, 0.2f), 0.95f);
                for (int w = 0; w < 6; w++) { mb.Box(new Vector3(x - 1.5f - w * 2.4f, 2.6f, 1.46f), new Vector3(1.6f, 0.9f, 0.02f)); mb.Box(new Vector3(x - 1.5f - w * 2.4f, 2.6f, -1.46f), new Vector3(1.6f, 0.9f, 0.02f)); }
                mb.SetColor(new Color(0.15f, 0.15f, 0.16f), 0.3f);
                mb.Box(new Vector3(x - 3f, 0.3f, 0), new Vector3(2.4f, 0.5f, 2.4f)); mb.Box(new Vector3(x - 12f, 0.3f, 0), new Vector3(2.4f, 0.5f, 2.4f));
            }
            mb.SetColor(new Color(1f, 0.95f, 0.75f), 0.9f);
            mb.Box(new Vector3(0.05f, 2.4f, 0.9f), new Vector3(0.05f, 0.3f, 0.4f)); mb.Box(new Vector3(0.05f, 2.4f, -0.9f), new Vector3(0.05f, 0.3f, 0.4f));
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Train");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(-31.5f, 2f, 0); bc.size = new Vector3(63.5f, 3.6f, 2.9f);
            var hl = new GameObject("Headlight").AddComponent<Light>(); hl.transform.SetParent(go.transform, false); hl.transform.localPosition = new Vector3(1f, 2.4f, 0); hl.type = LightType.Point; hl.range = 25f; hl.intensity = 2f; hl.color = new Color(1f, 0.95f, 0.8f);
            var tr = go.AddComponent<Train>();
            tr.from = fromX; tr.to = toX + 64f; tr.interval = interval;
            tr.rb = go.AddComponent<Rigidbody>(); tr.rb.isKinematic = true;
            tr.t = interval * 0.6f;
        }
        void FixedUpdate()
        {
            t += Time.fixedDeltaTime;
            float c = Mathf.Repeat(t, interval);
            const float runTime = 9f;
            float u = Mathf.Clamp01(c / runTime);
            if (c > runTime) { u = 0f; horned = false; }
            if (!horned && c < 0.2f) { horned = true; AudioManager.Play("train_horn", new Vector3(from + 30f, 1f, 0), 1f, 1f, 1f, 20f, 200f); }
            rb.MovePosition(new Vector3(Mathf.Lerp(from, to, u), -1.1f, 0));
        }
        void OnCollisionEnter(Collision c)
        {
            if (NetState.IsClient) return;
            var part = c.collider.GetComponent<BodyPart>();
            var p = c.collider.GetComponentInParent<PlayerController>();
            if (p != null) { p.Health.TakeDamage(new DamageInfo { Amount = 500f, Type = DamageType.Blunt, Point = c.GetContact(0).point, Direction = Vector3.right, Weapon = "Subway train" }); return; }
            if (part == null || part.Doll == null) return;
            part.Doll.Launch(new Vector3(28f, 6f, Random.Range(-3f, 3f)));
            Machines.Hurt(part, 260f, DamageType.Blunt, Vector3.right, this, "Subway train");
            if (part.Gibbable && part.Doll.Dismember != null && Random.value < 0.5f) part.Doll.Dismember.Gib(part, Vector3.right);
            BloodFX.Burst(c.GetContact(0).point, Vector3.right + Vector3.up * 0.3f, 3f, new Color(0.5f, 0.02f, 0.02f));
            AudioManager.Play("sledge_flesh", c.GetContact(0).point, 1f, 0.6f);
        }
    }

    /// <summary>The live third rail: zaps whatever touches it.</summary>
    public sealed class ThirdRail : MonoBehaviour
    {
        float next;
        public static void Create(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Third Rail Zone"); go.layer = Layers.Trigger;
            go.transform.SetParent(parent, false); go.transform.position = center;
            var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
            go.AddComponent<ThirdRail>();
        }
        void OnTriggerStay(Collider c)
        {
            if (Time.time < next) return;
            var part = c.GetComponent<BodyPart>();
            var pl = c.GetComponentInParent<PlayerController>();
            if (part == null && pl == null) return;
            next = Time.time + 0.2f;
            var at = c.ClosestPoint(transform.position);
            AudioManager.Play("zap", at, 1f, Random.Range(0.8f, 1.2f));
            FX.Sparks(at, Vector3.up, 14, 6f);
            FX.Flash(at, new Color(0.6f, 0.8f, 1f), 6f, 4f, 0.06f);
            if (pl != null) { pl.Health.TakeDamage(new DamageInfo { Amount = 25f, Type = DamageType.Generic, Point = at, Direction = Vector3.up, Weapon = "Third rail" }); pl.Launch(Vector3.up * 5f); return; }
            if (NetState.IsClient) return;
            Machines.Hurt(part, 40f, DamageType.Generic, Vector3.up, this, "Third rail");
            GoreKit.Burn(part, 0.25f);
            if (part.Body != null && !part.Body.isKinematic) part.Body.AddForce(Random.insideUnitSphere * 5f + Vector3.up * 3f, ForceMode.VelocityChange);
            if (part.Doll.Ragdoll != null) part.Doll.Ragdoll.Fall(3f);
        }
    }

    /// <summary>A steel girder hanging from the crane, swinging in a slow arc.</summary>
    public sealed class SwingingGirder : MonoBehaviour
    {
        public static void Create(Transform parent, Vector3 hook, float cable)
        {
            var root = new GameObject("Crane Hook"); root.transform.SetParent(parent, false); root.transform.position = hook;
            var anchor = root.AddComponent<Rigidbody>(); anchor.isKinematic = true;
            var g = new GameObject("Girder"); g.layer = Layers.Props; g.transform.SetParent(parent, false); g.transform.position = hook + Vector3.down * cable;
            var mb = new MeshBuilder { Color = new Color(0.65f, 0.3f, 0.15f), Gloss = 0.5f };
            mb.Box(Vector3.zero, new Vector3(8f, 0.5f, 0.4f));
            mb.SetColor(new Color(0.2f, 0.2f, 0.22f)); mb.Cylinder(Vector3.zero, Vector3.up * cable, 0.03f, 0.03f, 4);
            g.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Girder");
            g.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(8f, 0.5f, 0.4f);
            var rb = g.AddComponent<Rigidbody>(); rb.mass = 900f; rb.angularDrag = 0.3f;
            var j = g.AddComponent<ConfigurableJoint>();
            j.connectedBody = anchor; j.autoConfigureConnectedAnchor = false;
            j.anchor = Vector3.up * cable; j.connectedAnchor = Vector3.zero;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            rb.velocity = new Vector3(0, 0, 8f);
            g.AddComponent<WreckingBallHit>();
            g.AddComponent<SwingingGirder>().body = rb;
        }
        Rigidbody body;
        void FixedUpdate() { if (!NetState.IsClient && body != null && body.velocity.magnitude < 3f) body.AddForce(new Vector3(0, 0, Mathf.Sign(Mathf.Sin(Time.time * 0.4f)) * 1500f)); }
    }

    /// <summary>Cement mixer: the drum spins; anything that falls in gets churned and spat out.</summary>
    public sealed class CementMixer : MonoBehaviour
    {
        Transform drum; TriggerZone zone; float next;
        public static void Create(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Cement Mixer"); go.layer = Layers.Props; go.transform.SetParent(parent, false); go.transform.position = pos;
            var bmb = new MeshBuilder { Color = new Color(0.95f, 0.5f, 0.1f), Gloss = 0.5f };
            bmb.Box(new Vector3(0, 0.6f, 0), new Vector3(2.2f, 0.3f, 4f)); bmb.SetColor(new Color(0.15f, 0.15f, 0.16f)); bmb.Box(new Vector3(0, 0.25f, 0), new Vector3(2f, 0.5f, 3.6f));
            go.AddComponent<MeshFilter>().sharedMesh = bmb.ToMesh("Mixer Base"); go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.45f, 0); bc.size = new Vector3(2.2f, 0.9f, 4f);
            var cm = go.AddComponent<CementMixer>();
            var d = new GameObject("Drum"); d.layer = Layers.Props; d.transform.SetParent(go.transform, false); d.transform.localPosition = new Vector3(0, 2.1f, 0); d.transform.localRotation = Quaternion.Euler(-20f, 0, 0);
            var dmb = new MeshBuilder { Color = new Color(0.85f, 0.85f, 0.82f), Gloss = 0.4f };
            dmb.Cylinder(new Vector3(0, 0, -1.6f), new Vector3(0, 0, 0.6f), 0.9f, 1.1f, 16); dmb.Cylinder(new Vector3(0, 0, 0.6f), new Vector3(0, 0, 1.6f), 1.1f, 0.45f, 16);
            dmb.SetColor(new Color(0.95f, 0.5f, 0.1f)); for (int i = 0; i < 3; i++) dmb.Torus(new Vector3(0, 0, -1f + i * 0.8f), 1.0f + i * 0.05f, 0.05f, 16, 3, Vector3.forward);
            d.AddComponent<MeshFilter>().sharedMesh = dmb.ToMesh("Drum"); d.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            cm.drum = d.transform;
            cm.zone = Machines.Zone(d.transform, new Vector3(0, 0.2f, 1.7f), new Vector3(1f, 1f, 1f));
            AudioManager.CreateLoop("grind", go.transform, 0.35f, AudioBus.Sfx, 20f);
        }
        void Update()
        {
            if (drum != null) drum.Rotate(0, 0, 90f * Time.deltaTime, Space.Self);
            if (NetState.IsClient || Time.time < next) return;
            next = Time.time + 0.4f;
            foreach (var rb in new List<Rigidbody>(zone.Live()))
            {
                if (rb.isKinematic) continue;
                var part = rb.GetComponent<BodyPart>();
                if (part != null) { Machines.Hurt(part, 25f, DamageType.Crush, Vector3.up, this, "Cement mixer"); BloodFX.Mist(rb.position, Vector3.up, 0.8f); if (part.Doll != null) part.Doll.Launch(Vector3.up * 7f + Random.insideUnitSphere * 3f); }
                else rb.velocity = Vector3.up * 7f + Random.insideUnitSphere * 3f;
            }
        }
    }

    /// <summary>Drops a steel beam from the top floor every few seconds.</summary>
    public sealed class BeamDropper : MonoBehaviour
    {
        float interval, next; readonly List<GameObject> beams = new List<GameObject>();
        public static void Create(Transform parent, Vector3 pos, float interval)
        {
            var go = new GameObject("Beam Dropper"); go.transform.SetParent(parent, false); go.transform.position = pos;
            var bd = go.AddComponent<BeamDropper>(); bd.interval = interval; bd.next = Time.time + interval;
        }
        void Update()
        {
            if (NetState.IsClient || Time.time < next) return;
            next = Time.time + interval;
            var g = new GameObject("Falling Beam"); g.layer = Layers.Props;
            g.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : transform, false);
            g.transform.SetPositionAndRotation(transform.position + new Vector3(Random.Range(-8f, 8f), 0, Random.Range(-6f, 6f)), Quaternion.Euler(0, Random.Range(0f, 180f), Random.Range(-10f, 10f)));
            var mb = new MeshBuilder { Color = new Color(0.65f, 0.3f, 0.15f), Gloss = 0.5f };
            mb.Box(Vector3.zero, new Vector3(5f, 0.35f, 0.3f));
            g.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Beam"); g.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            g.AddComponent<BoxCollider>().size = new Vector3(5f, 0.35f, 0.3f);
            var rb = g.AddComponent<Rigidbody>(); rb.mass = 400f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            g.AddComponent<WreckingBallHit>();
            g.AddComponent<Surface>().Type = SurfaceType.Metal;
            AudioManager.Play("creak", transform.position, 1f, 0.7f, 1f, 5f, 80f);
            beams.Add(g);
            Debris.Register(g, 40f);
            if (beams.Count > 6) { if (beams[0] != null) Destroy(beams[0]); beams.RemoveAt(0); }
        }
    }

    /// <summary>Prison riot: prisoners and armed guards spawn and go at each other (and you).</summary>
    public static class Riot
    {
        static float last = -99f;
        public static void Start(Vector3 at)
        {
            if (NetState.IsClient || Time.time - last < 20f) return;
            last = Time.time;
            AudioManager.Play("buzzer", at + Vector3.up * 4f, 1f, 0.8f, 0.5f, 10f, 120f);
            GameEvents.ShowToast("RIOT! The prisoners are out of their cells.");
            for (int i = 0; i < 6; i++) GoredollFactory.Spawn(at + new Vector3(-15 + i * 6, 0, i % 2 == 0 ? 7.5f : -7.5f), Random.Range(0f, 360f), NpcTypes.Preset("prisoner"));
            for (int i = 0; i < 3; i++) GoredollFactory.Spawn(at + new Vector3(-6 + i * 6, 0, 0), Random.Range(0f, 360f), NpcTypes.Preset("cop"));
        }
    }
}
