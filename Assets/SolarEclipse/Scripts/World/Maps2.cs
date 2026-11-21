using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 maps: The Morgue (the intro's morgue, playable), Skyline (jump between rooftops high above the city),
    /// The Sewers (tunnels, sludge and a waterfall drop) and the Junkyard (car stacks, a crusher and a magnet crane).
    /// </summary>
    public static class Maps2
    {
        static Material T(Texture2D t) => MapBuilder.Tex(t);

        public static IEnumerable<MapDef> Defs()
        {
            yield return new MapDef
            {
                Id = "morgue", Name = "The Morgue", Size = "Small - indoor", Glyph = "skull", DefaultTime = TimeOfDay.Night, Indoor = true, Ambience = "amb_hum",
                Description = "The cold storage room from the Gore Sandbox 2 intro: drawers full of bodies, gurneys, a drain in the floor and flickering tubes.",
                Features = new[] { "Body drawers", "Gurneys", "Autopsy table", "Flickering lights" },
                ThemeTop = new Color(0.15f, 0.2f, 0.2f), ThemeBottom = new Color(0.55f, 0.62f, 0.58f), Accent = new Color(0.6f, 1f, 0.85f), Build = Morgue,
            };
            yield return new MapDef
            {
                Id = "skyline", Name = "Skyline", Size = "Medium - high above the city", Glyph = "building", DefaultTime = TimeOfDay.Sunset, Ambience = "amb_wind",
                Description = "Flat roofs of six buildings with planks and gaps between them. Fifty metres down to the street. Gravity does the rest.",
                Features = new[] { "Huge falls", "Planks between roofs", "AC units and water tanks", "Helipad" },
                ThemeTop = new Color(0.95f, 0.55f, 0.3f), ThemeBottom = new Color(0.25f, 0.2f, 0.3f), Accent = new Color(1f, 0.7f, 0.3f), Build = Rooftops,
            };
            yield return new MapDef
            {
                Id = "sewers", Name = "The Sewers", Size = "Medium - underground", Glyph = "drop", DefaultTime = TimeOfDay.Night, Indoor = true, Ambience = "amb_drone",
                Description = "Brick tunnels around a channel of sludge, walkways on both sides, a junction chamber and a drop into the overflow pit.",
                Features = new[] { "Sludge channel", "Junction chamber", "Overflow drop", "Ladders" },
                ThemeTop = new Color(0.1f, 0.12f, 0.08f), ThemeBottom = new Color(0.35f, 0.38f, 0.2f), Accent = new Color(0.6f, 0.8f, 0.3f), Build = Sewers,
            };
            yield return new MapDef
            {
                Id = "junkyard", Name = "Junkyard", Size = "Large - outdoor", Glyph = "car", DefaultTime = TimeOfDay.Day, Ambience = "amb_wind",
                Description = "Stacks of wrecked cars, tyre piles, a car crusher and a magnet crane, all behind a chain-link fence.",
                Features = new[] { "Car stacks", "Car crusher", "Magnet crane", "Drivable wrecks" },
                ThemeTop = new Color(0.6f, 0.7f, 0.85f), ThemeBottom = new Color(0.45f, 0.35f, 0.25f), Accent = new Color(0.95f, 0.6f, 0.2f), Build = Junkyard,
            };
            yield return new MapDef
            {
                Id = "lab", Name = "Laboratory", Size = "Medium - indoor", Glyph = "warning", DefaultTime = TimeOfDay.Night, Indoor = true, Ambience = "amb_hum",
                Description = "A sealed research lab: clean rooms behind glass, specimen tanks, a decontamination corridor and a containment cell.",
                Features = new[] { "Glass clean rooms", "Specimen tanks", "Containment cell", "Hazard lighting" },
                ThemeTop = new Color(0.85f, 0.9f, 0.95f), ThemeBottom = new Color(0.3f, 0.4f, 0.5f), Accent = new Color(0.3f, 1f, 0.6f), Build = Lab,
            };
            yield return new MapDef
            {
                Id = "colosseum", Name = "Colosseum", Size = "Medium - arena", Glyph = "target", DefaultTime = TimeOfDay.Day, Ambience = "amb_wind",
                Description = "A ring of stone tiers around a sand floor. Two gates, pillars to hide behind and nowhere to run.",
                Features = new[] { "Sand arena", "Stone tiers", "Gates", "Pillars" },
                ThemeTop = new Color(0.95f, 0.85f, 0.6f), ThemeBottom = new Color(0.65f, 0.5f, 0.3f), Accent = new Color(1f, 0.8f, 0.4f), Build = Colosseum,
            };
            yield return new MapDef
            {
                Id = "village", Name = "Medieval Village", Size = "Large - outdoor", Glyph = "home", DefaultTime = TimeOfDay.Sunset, Ambience = "amb_wind",
                Description = "Timber houses around a well, a chapel, market stalls, a gallows and a wall with a wooden gate.",
                Features = new[] { "Timber houses", "Market", "Gallows", "Village wall" },
                ThemeTop = new Color(0.9f, 0.6f, 0.35f), ThemeBottom = new Color(0.35f, 0.4f, 0.25f), Accent = new Color(0.9f, 0.75f, 0.4f), Build = Village,
            };
            yield return new MapDef
            {
                Id = "containerport", Name = "Container Port", Size = "Large - outdoor", Glyph = "box", DefaultTime = TimeOfDay.Night, Ambience = "amb_wind",
                Description = "Container stacks, a pier over black water, cranes and a warehouse. Sodium lamps and long shadows.",
                Features = new[] { "Container maze", "Pier", "Warehouse", "Deep water" },
                ThemeTop = new Color(0.08f, 0.1f, 0.18f), ThemeBottom = new Color(0.3f, 0.25f, 0.15f), Accent = new Color(1f, 0.65f, 0.25f), Build = Docks,
            };
            yield return new MapDef
            {
                Id = "flatgrass2", Name = "Flatgrass 2", Size = "Huge - empty", Glyph = "grid", DefaultTime = TimeOfDay.Day, Ambience = "amb_wind",
                Description = "A giant flat field with nothing in it. Bring your own chaos.",
                Features = new[] { "400 m across", "Nothing in the way", "Perfect for vehicles and creations" },
                ThemeTop = new Color(0.45f, 0.65f, 0.95f), ThemeBottom = new Color(0.35f, 0.6f, 0.3f), Accent = new Color(0.6f, 1f, 0.5f), Build = Flatgrass2,
            };
            yield return new MapDef
            {
                Id = "farm", Name = "Farm", Size = "Large - outdoor", Glyph = "home", DefaultTime = TimeOfDay.Day, Ambience = "amb_wind",
                Description = "A farmhouse, a big red barn with a hay loft, a silo, fields and fences. Tractors not included. Pitchforks are.",
                Features = new[] { "Barn with hay loft", "Silo", "Fields", "Farmhouse" },
                ThemeTop = new Color(0.55f, 0.75f, 0.95f), ThemeBottom = new Color(0.55f, 0.5f, 0.25f), Accent = new Color(0.85f, 0.25f, 0.2f), Build = Farm,
            };
            yield return new MapDef
            {
                Id = "gasstation", Name = "Desert Gas Station", Size = "Medium - outdoor", Glyph = "car", DefaultTime = TimeOfDay.Sunset, Ambience = "amb_wind",
                Description = "The last stop for 200 miles: pumps, a little shop, a motel sign and a lot of sand. The pumps explode.",
                Features = new[] { "Explosive pumps", "Shop", "Desert road", "Rocks and cacti" },
                ThemeTop = new Color(0.98f, 0.6f, 0.3f), ThemeBottom = new Color(0.85f, 0.7f, 0.45f), Accent = new Color(1f, 0.5f, 0.2f), Build = GasStation,
            };
            yield return new MapDef
            {
                Id = "cabin", Name = "Forest Cabin", Size = "Medium - outdoor", Glyph = "home", DefaultTime = TimeOfDay.Night, Ambience = "amb_wind",
                Description = "A lonely log cabin in a dark pine forest with a woodshed, a campfire and a lake. Classic.",
                Features = new[] { "Log cabin", "Dense forest", "Woodshed", "Lake" },
                ThemeTop = new Color(0.08f, 0.12f, 0.15f), ThemeBottom = new Color(0.15f, 0.25f, 0.15f), Accent = new Color(1f, 0.6f, 0.25f), Build = Cabin,
            };
        }

        // ------------------------------------------------------------------ The Morgue

        static void Morgue(MapBuilder m)
        {
            var tiles = TextureFactory.Tiles; var metal = T(TextureFactory.Metal);
            Color wall = new Color(0.55f, 0.62f, 0.58f);
            m.Block("Floor", new Vector3(-8, -0.3f, -10), new Vector3(8, 0, 10), T(tiles), SurfaceType.Concrete, new Color(0.42f, 0.46f, 0.44f));
            m.Block("Ceiling", new Vector3(-8, 3.6f, -10), new Vector3(8, 3.9f, 10), T(tiles), SurfaceType.Concrete, new Color(0.3f, 0.32f, 0.31f));
            m.Block("Wall", new Vector3(-8.3f, 0, -10), new Vector3(-8, 3.6f, 10), T(tiles), SurfaceType.Concrete, wall);
            m.Block("Wall", new Vector3(8, 0, -10), new Vector3(8.3f, 3.6f, 10), T(tiles), SurfaceType.Concrete, wall);
            m.Block("Wall", new Vector3(-8, 0, 10), new Vector3(8, 3.6f, 10.3f), T(tiles), SurfaceType.Concrete, wall);
            m.Block("Wall", new Vector3(-8, 0, -10.3f), new Vector3(8, 3.6f, -10), T(tiles), SurfaceType.Concrete, wall);
            for (int side = -1; side <= 1; side += 2)
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 8; c++)
                    {
                        var p = new Vector3(side * 7.9f, 0.55f + r * 0.95f, -8f + c * 2.1f);
                        m.Box("Drawer", p, Quaternion.identity, new Vector3(0.16f, 0.85f, 1.9f), metal, SurfaceType.Metal, new Color(0.62f, 0.64f, 0.66f));
                        m.Box("Handle", p - new Vector3(side * 0.1f, -0.2f, 0), Quaternion.identity, new Vector3(0.05f, 0.04f, 0.5f), Mats.LitMetal, SurfaceType.Metal, new Color(0.8f, 0.8f, 0.82f), 1f, false);
                    }
            m.Box("Autopsy Table", new Vector3(0, 0.9f, 4f), Quaternion.identity, new Vector3(1f, 0.1f, 2.2f), Mats.LitMetal, SurfaceType.Metal, new Color(0.75f, 0.77f, 0.8f));
            m.Box("Table Post", new Vector3(0, 0.45f, 4f), Quaternion.identity, new Vector3(0.3f, 0.9f, 0.3f), Mats.LitMetal, SurfaceType.Metal, new Color(0.55f, 0.56f, 0.58f));
            m.Cylinder("Drain", new Vector3(0, 0.005f, 0), 0.22f, 0.01f, Mats.LitMetal, SurfaceType.Metal, new Color(0.15f, 0.15f, 0.16f), 16, false);
            for (int i = 0; i < 3; i++) m.CeilingLight(new Vector3(0, 3.5f, -6 + i * 6), new Color(0.85f, 1f, 0.95f), 10f, 1.5f, new Vector3(0.18f, 0.05f, 1.6f));
            for (int i = 0; i < 4; i++) HospitalBed.Create(new Vector3(i % 2 == 0 ? -3f : 3f, 0, -5f + (i / 2) * 5f), Quaternion.identity);
            for (int i = 0; i < 10; i++) Decals.Blood(new Vector3(Random.Range(-6f, 6f), 0.01f, Random.Range(-8f, 8f)), Vector3.up, Random.Range(0.2f, 0.8f), Random.value < 0.4f ? 3 : 0, null);
            m.PlayerSpawn = new Vector3(0, 0.1f, -8.5f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-3, 0.1f, 2), new Vector3(3, 0.1f, 2), new Vector3(0, 0.1f, 7), new Vector3(-4, 0.1f, 7) });
            m.Playable = new Bounds(new Vector3(0, 1.8f, 0), new Vector3(16, 4, 20));
        }

        // ------------------------------------------------------------------ Rooftops

        static void Rooftops(MapBuilder m)
        {
            m.Block("Street", new Vector3(-80, -51, -80), new Vector3(80, -50, 80), T(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.3f, 0.3f, 0.32f), 0.1f);
            var roofs = new[] { new Vector4(-18, -14, 14, 0), new Vector4(4, -16, 16, 3), new Vector4(26, -12, 12, -2), new Vector4(-20, 10, 16, 4), new Vector4(2, 12, 14, 1), new Vector4(24, 14, 14, 6) };
            var tints = new[] { new Color(0.55f, 0.45f, 0.4f), new Color(0.45f, 0.48f, 0.52f), new Color(0.6f, 0.55f, 0.45f) };
            for (int i = 0; i < roofs.Length; i++)
            {
                var r = roofs[i]; float half = r.z / 2f, top = r.w;
                m.Building(new Vector3(r.x - half, -50, r.y - half), new Vector3(r.x + half, top, r.y + half), i % 2 == 0 ? TextureFactory.Brick : TextureFactory.Plaster, tints[i % 3], true);
                m.Block("Roof", new Vector3(r.x - half, top, r.y - half), new Vector3(r.x + half, top + 0.2f, r.y + half), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.45f, 0.45f, 0.47f));
                m.Block("Parapet", new Vector3(r.x - half, top + 0.2f, r.y + half - 0.3f), new Vector3(r.x + half, top + 1f, r.y + half), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.5f, 0.5f, 0.52f));
                m.Box("AC Unit", new Vector3(r.x + half * 0.4f, top + 0.8f, r.y - half * 0.3f), Quaternion.identity, new Vector3(1.6f, 1.2f, 1.2f), Mats.LitMetal, SurfaceType.Metal, new Color(0.65f, 0.66f, 0.68f));
                if (i % 2 == 1) m.Cylinder("Water Tank", new Vector3(r.x - half * 0.5f, top + 0.2f, r.y), 1.4f, 2.6f, T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.5f, 0.35f, 0.22f), 16);
            }
            // Planks across the gaps.
            void Plank(Vector3 a, Vector3 b) { var mid = (a + b) / 2f; var d = b - a; m.Box("Plank", mid, Quaternion.LookRotation(d), new Vector3(0.45f, 0.08f, d.magnitude + 0.6f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.55f, 0.4f, 0.25f)); }
            Plank(new Vector3(-11, 0.25f, -14), new Vector3(-4, 3.2f, -15));
            Plank(new Vector3(12, 3.2f, -14), new Vector3(20, 0.1f, -12));
            Plank(new Vector3(-18, 0.25f, -7), new Vector3(-20, 4.25f, 2));
            Plank(new Vector3(2, 3.25f, -8), new Vector3(2, 1.25f, 5));
            Plank(new Vector3(-12, 4.25f, 11), new Vector3(-5, 1.25f, 12));
            Plank(new Vector3(9, 1.25f, 13), new Vector3(17, 6.25f, 14));
            m.Cylinder("Helipad", new Vector3(24, 6.2f, 14), 5f, 0.05f, Mats.Lit, SurfaceType.Concrete, new Color(0.25f, 0.27f, 0.3f), 24, false);
            m.PlayerSpawn = new Vector3(-18, 0.4f, -16f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-16, 0.4f, -12), new Vector3(4, 3.4f, -16), new Vector3(26, 0.4f, -12), new Vector3(-20, 4.4f, 10), new Vector3(2, 1.4f, 12), new Vector3(22, 6.4f, 12) });
            m.Playable = new Bounds(new Vector3(0, -20, 0), new Vector3(110, 80, 110));
            m.KillY = -60f;
        }

        // ------------------------------------------------------------------ The Sewers

        static void Sewers(MapBuilder m)
        {
            var brick = TextureFactory.Brick; Color b = new Color(0.32f, 0.3f, 0.25f);
            void Tunnel(Vector3 c, float len, bool alongZ)
            {
                Vector3 ax = alongZ ? Vector3.forward : Vector3.right, side = alongZ ? Vector3.right : Vector3.forward;
                Vector3 S(float s, float y, float a) => c + side * s + Vector3.up * y + ax * a;
                Vector3 Mn(Vector3 p, Vector3 q) => Vector3.Min(p, q); Vector3 Mx(Vector3 p, Vector3 q) => Vector3.Max(p, q);
                void Blk(string n, Vector3 p, Vector3 q, Texture2D t, Color col, SurfaceType st) => m.Block(n, Mn(p, q), Mx(p, q), T(t), st, col);
                Blk("Channel", S(-1.5f, -1.5f, -len / 2), S(1.5f, -1.2f, len / 2), TextureFactory.Dirt, new Color(0.25f, 0.28f, 0.12f), SurfaceType.Dirt);
                Blk("Walkway", S(-4f, -0.3f, -len / 2), S(-1.5f, 0f, len / 2), brick, b, SurfaceType.Concrete);
                Blk("Walkway", S(1.5f, -0.3f, -len / 2), S(4f, 0f, len / 2), brick, b, SurfaceType.Concrete);
                Blk("Channel Wall", S(-1.6f, -1.5f, -len / 2), S(-1.5f, 0f, len / 2), brick, b * 0.8f, SurfaceType.Concrete);
                Blk("Channel Wall", S(1.5f, -1.5f, -len / 2), S(1.6f, 0f, len / 2), brick, b * 0.8f, SurfaceType.Concrete);
                Blk("Wall", S(-4.3f, 0f, -len / 2), S(-4f, 3.6f, len / 2), brick, b, SurfaceType.Concrete);
                Blk("Wall", S(4f, 0f, -len / 2), S(4.3f, 3.6f, len / 2), brick, b, SurfaceType.Concrete);
                Blk("Roof", S(-4.3f, 3.6f, -len / 2), S(4.3f, 4f, len / 2), brick, b * 0.7f, SurfaceType.Concrete);
                m.Box("Sludge", S(0, -1.25f, 0), alongZ ? Quaternion.identity : Quaternion.Euler(0, 90, 0), new Vector3(2.98f, 0.02f, len), Mats.Glow(new Color(0.25f, 0.35f, 0.08f), 0.3f), SurfaceType.Water, null, 1f, false);
                for (float a = -len / 2 + 5; a < len / 2; a += 10) m.CeilingLight(S(0, 3.4f, a), new Color(1f, 0.85f, 0.5f), 10f, 1.1f, new Vector3(0.4f, 0.1f, 0.4f));
            }
            Tunnel(new Vector3(0, 0, -25), 40f, true);
            Tunnel(new Vector3(25, 0, 0), 40f, false);
            Tunnel(new Vector3(-25, 0, 0), 40f, false);
            // Junction chamber with an overflow pit.
            m.Block("Chamber Floor", new Vector3(-5, -0.3f, -5), new Vector3(5, 0, 5), T(brick), SurfaceType.Concrete, b);
            m.Block("Chamber Roof", new Vector3(-5, 5.5f, -5), new Vector3(5, 6f, 5), T(brick), SurfaceType.Concrete, b * 0.7f);
            m.Block("Chamber Wall", new Vector3(-5, 0, 5), new Vector3(5, 5.5f, 5.3f), T(brick), SurfaceType.Concrete, b);
            m.Block("Pit Floor", new Vector3(-2.5f, -9, 0.5f), new Vector3(2.5f, -8.7f, 4.8f), T(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.2f, 0.22f, 0.1f));
            m.Block("Pit Wall", new Vector3(-2.6f, -9, 0.4f), new Vector3(2.6f, -0.3f, 0.5f), T(brick), SurfaceType.Concrete, b * 0.6f);
            m.CeilingLight(new Vector3(0, 5.3f, 0), new Color(1f, 0.8f, 0.5f), 12f, 1.4f);
            m.PlayerSpawn = new Vector3(-2.5f, 0.1f, -38f);
            m.NpcSpawns.AddRange(new[] { new Vector3(2.5f, 0.1f, -30), new Vector3(-2.5f, 0.1f, -15), new Vector3(30, 0.1f, 2.5f), new Vector3(-30, 0.1f, -2.5f), new Vector3(0, 0.1f, -2) });
            m.Playable = new Bounds(new Vector3(0, 0, -10), new Vector3(100, 20, 60));
            m.KillY = -20f;
        }

        // ------------------------------------------------------------------ Junkyard

        static void Junkyard(MapBuilder m)
        {
            m.Block("Ground", new Vector3(-50, -1, -50), new Vector3(50, 0, 50), T(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.4f, 0.33f, 0.25f), 0.15f);
            m.Fence(new Vector3(-45, 0, -45), new Vector3(45, 0, -45), 3f); m.Fence(new Vector3(-45, 0, 45), new Vector3(45, 0, 45), 3f);
            m.Fence(new Vector3(-45, 0, -45), new Vector3(-45, 0, 45), 3f); m.Fence(new Vector3(45, 0, -45), new Vector3(45, 0, 45), 3f);
            var rust = new[] { new Color(0.45f, 0.25f, 0.15f), new Color(0.3f, 0.32f, 0.35f), new Color(0.5f, 0.12f, 0.1f), new Color(0.2f, 0.3f, 0.45f), new Color(0.55f, 0.5f, 0.3f) };
            for (int s = 0; s < 10; s++)
            {
                var basePos = new Vector3(-35 + (s % 5) * 17f, 0, s < 5 ? -25f : 25f);
                int h = Random.Range(2, 5);
                for (int k = 0; k < h; k++)
                    m.Box("Wreck", basePos + new Vector3(Random.Range(-0.3f, 0.3f), 0.65f + k * 1.3f, 0), Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-15f, 15f), Random.Range(-3f, 3f)), new Vector3(1.9f, 1.25f, 4.3f), Mats.LitMetal, SurfaceType.Metal, rust[Random.Range(0, rust.Length)]);
            }
            for (int t = 0; t < 6; t++)
            {
                var c = new Vector3(Random.Range(-30f, 30f), 0, Random.Range(-10f, 10f));
                for (int k = 0; k < 5; k++) PropFactory.Spawn("tire", c + new Vector3(Random.Range(-1f, 1f), 0.4f + k * 0.35f, Random.Range(-1f, 1f)), Quaternion.Euler(90, 0, 0));
            }
            CrusherCeiling.Create(new Vector3(30, 0, 0), Quaternion.identity);
            MagnetCrane.Create(new Vector3(-30, 0, 0), Quaternion.identity);
            foreach (var id in new[] { "c_sedan", "c_hatch", "c_pickup" })
                PropFactory.Spawn(OWCar.DefFor(id, "", "", false, false, false), new Vector3(Random.Range(-15f, 15f), 1f, Random.Range(-12f, 12f)), Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            m.StreetLamp(new Vector3(0, 0, -40), 0f, new Color(1f, 0.85f, 0.6f), 7f, 24f, 2f);
            m.PlayerSpawn = new Vector3(0, 0.1f, -40f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-10, 0.1f, 0), new Vector3(10, 0.1f, 5), new Vector3(0, 0.1f, 15), new Vector3(-20, 0.1f, 10), new Vector3(20, 0.1f, -10) });
            m.Playable = new Bounds(new Vector3(0, 5, 0), new Vector3(92, 20, 92));
        }

        // ------------------------------------------------------------------ Laboratory

        static void Room(MapBuilder m, Vector3 min, Vector3 max, Texture2D tex, Color c, SurfaceType st = SurfaceType.Concrete)
        {
            m.Block("Floor", new Vector3(min.x, min.y - 0.3f, min.z), new Vector3(max.x, min.y, max.z), T(tex), st, c * 0.8f);
            m.Block("Ceiling", new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y + 0.3f, max.z), T(tex), st, c * 0.6f);
            m.Block("Wall", new Vector3(min.x - 0.3f, min.y, min.z), new Vector3(min.x, max.y, max.z), T(tex), st, c);
            m.Block("Wall", new Vector3(max.x, min.y, min.z), new Vector3(max.x + 0.3f, max.y, max.z), T(tex), st, c);
            m.Block("Wall", new Vector3(min.x, min.y, max.z), new Vector3(max.x, max.y, max.z + 0.3f), T(tex), st, c);
            m.Block("Wall", new Vector3(min.x, min.y, min.z - 0.3f), new Vector3(max.x, max.y, min.z), T(tex), st, c);
        }

        static void Lab(MapBuilder m)
        {
            Room(m, new Vector3(-15, 0, -15), new Vector3(15, 4, 15), TextureFactory.Tiles, new Color(0.85f, 0.88f, 0.9f));
            // Glass clean rooms along both sides.
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                {
                    float z = -10 + i * 9f, x = side * 8f;
                    m.Box("Clean Room Glass", new Vector3(x, 2, z), Quaternion.identity, new Vector3(0.08f, 4f, 6f), Mats.Glass, SurfaceType.Glass, Color.white, 1f);
                    m.Block("Divider", new Vector3(Mathf.Min(x, side * 15f), 0, z + 3f), new Vector3(Mathf.Max(x, side * 15f), 4, z + 3.2f), T(TextureFactory.Tiles), SurfaceType.Concrete, new Color(0.8f, 0.82f, 0.85f));
                    m.Box("Lab Bench", new Vector3(side * 12f, 0.5f, z), Quaternion.identity, new Vector3(1.2f, 1f, 3.5f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.9f, 0.92f));
                    m.CeilingLight(new Vector3(side * 11.5f, 3.9f, z), new Color(0.85f, 0.95f, 1f), 9f, 1.4f, new Vector3(1.4f, 0.05f, 0.3f));
                }
            // Specimen tanks down the middle (glowing liquid).
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(0, 0, -9 + i * 6f);
                m.Cylinder("Tank Base", p, 0.9f, 0.3f, Mats.LitMetal, SurfaceType.Metal, new Color(0.4f, 0.42f, 0.45f), 16);
                m.Cylinder("Tank Liquid", p + Vector3.up * 0.3f, 0.75f, 2.6f, Mats.Glow(new Color(0.2f, 0.9f, 0.5f), 0.6f), SurfaceType.Glass, null, 16);
                m.Cylinder("Tank Cap", p + Vector3.up * 2.9f, 0.9f, 0.25f, Mats.LitMetal, SurfaceType.Metal, new Color(0.4f, 0.42f, 0.45f), 16);
                var l = new GameObject("Tank Light").AddComponent<Light>(); l.transform.SetParent(m.Root, false); l.transform.position = p + new Vector3(0, 1.6f, -1.2f);
                l.type = LightType.Point; l.color = new Color(0.3f, 1f, 0.5f); l.range = 5f; l.intensity = 1.2f; l.shadows = LightShadows.None;
            }
            // Containment cell at the back with a hazard stripe.
            m.Block("Containment Floor", new Vector3(-4, 0, 12), new Vector3(4, 0.02f, 15), Mats.Glow(new Color(0.9f, 0.7f, 0.05f), 0.2f), SurfaceType.Metal, null);
            for (int i = 0; i < 9; i++) m.Box("Bars", new Vector3(-4 + i, 2, 12), Quaternion.identity, new Vector3(0.08f, 4f, 0.08f), Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f));
            m.CeilingLight(new Vector3(0, 3.9f, 13.5f), new Color(1f, 0.25f, 0.2f), 8f, 1.6f);
            m.CeilingLight(new Vector3(0, 3.9f, -12f), new Color(0.9f, 0.95f, 1f), 10f, 1.4f);
            m.PlayerSpawn = new Vector3(0, 0.1f, -13.5f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-12, 0.1f, -10), new Vector3(12, 0.1f, -1), new Vector3(-12, 0.1f, 8), new Vector3(2, 0.1f, 13.5f), new Vector3(3, 0.1f, 0) });
            m.Playable = new Bounds(new Vector3(0, 2, 0), new Vector3(30, 4, 30));
        }

        // ------------------------------------------------------------------ Colosseum

        static void Colosseum(MapBuilder m)
        {
            Color stone = new Color(0.78f, 0.7f, 0.55f);
            m.Block("Sand", new Vector3(-60, -1, -60), new Vector3(60, 0, 60), T(TextureFactory.Sand), SurfaceType.Dirt, new Color(0.9f, 0.8f, 0.6f), 0.2f);
            int segs = 36; float r0 = 24f;
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs; var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); var rot = Quaternion.LookRotation(dir);
                bool gate = i == 0 || i == segs / 2;
                if (!gate) m.Box("Arena Wall", dir * r0 + Vector3.up * 1.75f, rot, new Vector3(4.4f, 3.5f, 0.8f), T(TextureFactory.Brick), SurfaceType.Concrete, stone);
                else m.Box("Gate", dir * r0 + Vector3.up * 1.75f, rot, new Vector3(4.2f, 3.5f, 0.2f), Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.22f, 0.2f), 0.5f, false);
                for (int t = 0; t < 5; t++)
                    m.Box("Tier", dir * (r0 + 1.4f + t * 1.6f) + Vector3.up * (3.5f + t * 1.1f) / 2f, rot, new Vector3(4.5f + t * 0.6f, 3.5f + t * 1.1f, 1.6f), T(TextureFactory.Concrete), SurfaceType.Concrete, stone * (0.9f + t * 0.03f));
                if (i % 3 == 0) m.Cylinder("Column", dir * (r0 + 9.5f) + Vector3.up * 9f, 0.6f, 6f, T(TextureFactory.Concrete), SurfaceType.Concrete, stone, 12);
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + 0.3f; var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 12f;
                m.Cylinder("Pillar", p, 0.8f, Random.Range(1.5f, 4.5f), T(TextureFactory.Concrete), SurfaceType.Concrete, stone, 12);
            }
            for (int i = 0; i < 12; i++) Decals.Blood(new Vector3(Random.Range(-15f, 15f), 0.01f, Random.Range(-15f, 15f)), Vector3.up, Random.Range(0.4f, 1.2f), Random.value < 0.5f ? 3 : 0, null);
            m.PlayerSpawn = new Vector3(0, 0.1f, -20f);
            m.NpcSpawns.AddRange(new[] { new Vector3(0, 0.1f, 20), new Vector3(8, 0.1f, 8), new Vector3(-8, 0.1f, 8), new Vector3(10, 0.1f, -5), new Vector3(-10, 0.1f, -5) });
            m.Playable = new Bounds(new Vector3(0, 5, 0), new Vector3(52, 20, 52));
        }

        // ------------------------------------------------------------------ Medieval Village

        static void Hut(MapBuilder m, Vector3 c, float yaw, float w, float d, float h, Color wall)
        {
            var rot = Quaternion.Euler(0, yaw, 0); var wood = T(TextureFactory.Wood); var plaster = T(TextureFactory.Plaster);
            Color beam = new Color(0.32f, 0.2f, 0.12f);
            m.Box("Wall", c + rot * new Vector3(0, h / 2, d / 2), rot, new Vector3(w, h, 0.25f), plaster, SurfaceType.Concrete, wall);
            m.Box("Wall", c + rot * new Vector3(-w / 2, h / 2, 0), rot, new Vector3(0.25f, h, d), plaster, SurfaceType.Concrete, wall);
            m.Box("Wall", c + rot * new Vector3(w / 2, h / 2, 0), rot, new Vector3(0.25f, h, d), plaster, SurfaceType.Concrete, wall);
            float side = (w - 1.4f) / 2f;
            m.Box("Wall", c + rot * new Vector3(-w / 2 + side / 2, h / 2, -d / 2), rot, new Vector3(side, h, 0.25f), plaster, SurfaceType.Concrete, wall);
            m.Box("Wall", c + rot * new Vector3(w / 2 - side / 2, h / 2, -d / 2), rot, new Vector3(side, h, 0.25f), plaster, SurfaceType.Concrete, wall);
            m.Box("Lintel", c + rot * new Vector3(0, h - 0.4f, -d / 2), rot, new Vector3(1.4f, 0.8f, 0.3f), wood, SurfaceType.Wood, beam);
            m.Box("Floor", c + rot * new Vector3(0, 0.05f, 0), rot, new Vector3(w, 0.1f, d), wood, SurfaceType.Wood, beam * 1.4f);
            for (int s = -1; s <= 1; s += 2)
                m.Box("Roof", c + rot * new Vector3(s * w / 4f, h + w / 4.5f, 0), rot * Quaternion.Euler(0, 0, -s * 38f), new Vector3(w * 0.68f, 0.18f, d + 0.8f), wood, SurfaceType.Wood, new Color(0.45f, 0.35f, 0.2f));
            m.Box("Beam", c + rot * new Vector3(0, h, -d / 2 - 0.13f), rot, new Vector3(w, 0.2f, 0.05f), wood, SurfaceType.Wood, beam, 0.5f, false);
        }

        static void Village(MapBuilder m)
        {
            m.Block("Ground", new Vector3(-70, -1, -70), new Vector3(70, 0, 70), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.45f, 0.55f, 0.3f), 0.12f);
            m.Block("Square", new Vector3(-12, 0, -12), new Vector3(12, 0.05f, 12), T(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.55f, 0.45f, 0.32f), 0.2f);
            var walls = new[] { new Color(0.9f, 0.85f, 0.72f), new Color(0.85f, 0.8f, 0.65f), new Color(0.8f, 0.72f, 0.6f) };
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f; var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 22f;
                Hut(m, p, a * Mathf.Rad2Deg + 180f, Random.Range(6f, 8f), Random.Range(5f, 7f), 3f, walls[i % 3]);
            }
            Hut(m, new Vector3(0, 0, 38), 180f, 8f, 14f, 6f, new Color(0.75f, 0.72f, 0.68f)); // chapel
            m.Box("Chapel Tower", new Vector3(0, 6, 46), Quaternion.identity, new Vector3(3f, 12f, 3f), T(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.65f, 0.62f, 0.58f));
            m.Cylinder("Well", new Vector3(0, 0, 0), 1.2f, 0.9f, T(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.6f, 0.58f, 0.55f), 14);
            for (int s = -1; s <= 1; s += 2) m.Box("Well Post", new Vector3(s * 1f, 1.5f, 0), Quaternion.identity, new Vector3(0.15f, 2f, 0.15f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.4f, 0.28f, 0.15f));
            m.Box("Well Roof", new Vector3(0, 2.6f, 0), Quaternion.identity, new Vector3(2.6f, 0.15f, 1.6f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.45f, 0.32f, 0.18f));
            // Market stalls.
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(-7.5f + i * 5f, 0, -8f);
                m.Box("Stall Table", p + Vector3.up * 0.45f, Quaternion.identity, new Vector3(3f, 0.9f, 1.2f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.5f, 0.35f, 0.2f));
                m.Box("Awning", p + new Vector3(0, 2.3f, 0.3f), Quaternion.Euler(-15, 0, 0), new Vector3(3.2f, 0.05f, 2f), Mats.Lit, SurfaceType.Carpet, i % 2 == 0 ? new Color(0.75f, 0.2f, 0.15f) : new Color(0.85f, 0.8f, 0.6f), 1f, false);
            }
            // Gallows.
            var gp = new Vector3(8f, 0, 6f); var wd = T(TextureFactory.Wood); Color gw = new Color(0.35f, 0.24f, 0.14f);
            m.Box("Gallows Platform", gp + Vector3.up * 1f, Quaternion.identity, new Vector3(4f, 2f, 4f), wd, SurfaceType.Wood, gw);
            m.Box("Gallows Post", gp + new Vector3(-1.5f, 4f, 0), Quaternion.identity, new Vector3(0.3f, 4f, 0.3f), wd, SurfaceType.Wood, gw);
            m.Box("Gallows Arm", gp + new Vector3(-0.3f, 5.85f, 0), Quaternion.identity, new Vector3(2.7f, 0.3f, 0.3f), wd, SurfaceType.Wood, gw);
            m.Stairs("Gallows Steps", gp + new Vector3(0, 0, -2f - 2.4f), 0f, 1.2f, 6, 0.33f, 0.4f, wd, SurfaceType.Wood, gw);
            // Village wall (palisade) with a gate gap at the south.
            for (int i = 0; i < 56; i++)
            {
                float a = i * Mathf.PI * 2f / 56f; if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 180f)) < 7f) continue;
                var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                m.Box("Palisade", dir * 55f + Vector3.up * 2.5f, Quaternion.LookRotation(dir), new Vector3(6.4f, 5f, 0.6f), wd, SurfaceType.Wood, new Color(0.4f, 0.3f, 0.2f));
            }
            for (int i = 0; i < 30; i++) { var p = new Vector3(Random.Range(-50f, 50f), 0, Random.Range(-50f, 50f)); if (p.magnitude > 30f && p.magnitude < 52f) m.Tree(p, Random.Range(0.8f, 1.3f), Random.value < 0.4f); }
            m.PlayerSpawn = new Vector3(0, 0.1f, -50f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-5, 0.1f, -4), new Vector3(5, 0.1f, 4), new Vector3(-10, 0.1f, 10), new Vector3(15, 0.1f, -10), new Vector3(0, 0.1f, 32) });
            m.Playable = new Bounds(new Vector3(0, 5, 0), new Vector3(112, 30, 112));
        }

        // ------------------------------------------------------------------ Docks

        static void Docks(MapBuilder m)
        {
            m.Block("Water", new Vector3(-90, -6, 10), new Vector3(90, -2.2f, 90), Mats.Glow(new Color(0.02f, 0.05f, 0.08f), 0.1f), SurfaceType.Water, null, 0.1f, false);
            m.Block("Seabed", new Vector3(-90, -7, 10), new Vector3(90, -6, 90), T(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.15f, 0.15f, 0.12f));
            m.Block("Quay", new Vector3(-70, -1, -60), new Vector3(70, 0, 10), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.42f, 0.42f, 0.43f), 0.15f);
            m.Block("Pier", new Vector3(-4, -0.4f, 10), new Vector3(4, 0, 50), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.38f, 0.3f, 0.22f));
            for (int z = 14; z <= 50; z += 6) for (int s = -1; s <= 1; s += 2) m.Cylinder("Pile", new Vector3(s * 3.7f, -6, z), 0.25f, 6f, T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.3f, 0.24f, 0.18f), 8);
            var cols = new[] { new Color(0.7f, 0.2f, 0.15f), new Color(0.15f, 0.35f, 0.6f), new Color(0.2f, 0.5f, 0.3f), new Color(0.8f, 0.55f, 0.15f), new Color(0.55f, 0.55f, 0.58f) };
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 6; col++)
                {
                    if ((row + col) % 4 == 1) continue; // alleys through the stacks
                    var p = new Vector3(-45 + col * 8f, 0, -40 + row * 9f); int h = Random.Range(1, 4);
                    for (int k = 0; k < h; k++) m.Box("Container", p + Vector3.up * (1.3f + k * 2.6f), Quaternion.Euler(0, Random.Range(-2f, 2f), 0), new Vector3(2.5f, 2.6f, 6.1f), Mats.LitMetal, SurfaceType.Metal, cols[Random.Range(0, cols.Length)]);
                }
            m.Building(new Vector3(20, 0, -45), new Vector3(55, 10, -20), TextureFactory.Metal, new Color(0.5f, 0.52f, 0.55f), false);
            for (int i = 0; i < 2; i++)
            {
                var b = new Vector3(-30 + i * 40f, 0, 5);
                for (int s = -1; s <= 1; s += 2) m.Box("Crane Leg", b + new Vector3(s * 4f, 10, 0), Quaternion.identity, new Vector3(0.8f, 20f, 0.8f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.6f, 0.1f));
                m.Box("Crane Boom", b + new Vector3(0, 20, 10), Quaternion.identity, new Vector3(9f, 1.4f, 32f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.6f, 0.1f));
            }
            for (int i = 0; i < 8; i++) m.StreetLamp(new Vector3(-60 + i * 17f, 0, 7f), 180f, new Color(1f, 0.65f, 0.3f), 7f, 22f, 2.2f);
            for (int i = 0; i < 4; i++) PropFactory.Spawn("barrel", new Vector3(Random.Range(-10f, 10f), 0.6f, Random.Range(-10f, 0f)), Quaternion.identity);
            m.PlayerSpawn = new Vector3(0, 0.1f, -55f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-20, 0.1f, -15), new Vector3(10, 0.1f, -5), new Vector3(0, 0.1f, 30), new Vector3(30, 0.1f, -30), new Vector3(-40, 0.1f, 0) });
            m.Playable = new Bounds(new Vector3(0, 5, 10), new Vector3(140, 40, 140));
            m.KillY = -8f;
        }

        // ------------------------------------------------------------------ Flatgrass 2

        static void Flatgrass2(MapBuilder m)
        {
            m.Block("Ground", new Vector3(-200, -1, -200), new Vector3(200, 0, 200), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.4f, 0.62f, 0.3f), 0.1f);
            m.Boundaries(new Bounds(new Vector3(0, 30, 0), new Vector3(400, 60, 400)));
            m.PlayerSpawn = new Vector3(0, 0.1f, 0);
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f; m.NpcSpawns.Add(new Vector3(Mathf.Sin(a), 0.01f, Mathf.Cos(a)) * 15f); }
            m.Playable = new Bounds(new Vector3(0, 30, 0), new Vector3(400, 60, 400));
        }

        // ------------------------------------------------------------------ Farm

        static void Farm(MapBuilder m)
        {
            m.Block("Ground", new Vector3(-80, -1, -80), new Vector3(80, 0, 80), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.5f, 0.6f, 0.3f), 0.12f);
            for (int f = 0; f < 3; f++)
                for (int r = 0; r < 10; r++)
                    m.Block("Crop Row", new Vector3(-60 + f * 18f, 0, 10 + r * 4f), new Vector3(-46 + f * 18f, 0.5f, 11.2f + r * 4f), T(TextureFactory.Dirt), SurfaceType.Dirt, f == 1 ? new Color(0.8f, 0.7f, 0.25f) : new Color(0.35f, 0.5f, 0.2f), 0.5f, false);
            // Barn (open front) with a hay loft.
            var wood = T(TextureFactory.Wood); Color red = new Color(0.62f, 0.15f, 0.12f);
            Vector3 b0 = new Vector3(15, 0, -10), b1 = new Vector3(35, 9, 10);
            m.Block("Barn Floor", new Vector3(b0.x, -0.05f, b0.z), new Vector3(b1.x, 0.05f, b1.z), wood, SurfaceType.Wood, new Color(0.45f, 0.35f, 0.22f));
            m.Block("Barn Wall", new Vector3(b0.x, 0, b1.z), new Vector3(b1.x, b1.y, b1.z + 0.3f), wood, SurfaceType.Wood, red);
            m.Block("Barn Wall", new Vector3(b0.x - 0.3f, 0, b0.z), new Vector3(b0.x, b1.y, b1.z), wood, SurfaceType.Wood, red);
            m.Block("Barn Wall", new Vector3(b1.x, 0, b0.z), new Vector3(b1.x + 0.3f, b1.y, b1.z), wood, SurfaceType.Wood, red);
            m.Block("Barn Front", new Vector3(b0.x, 0, b0.z - 0.3f), new Vector3(b0.x + 6, b1.y, b0.z), wood, SurfaceType.Wood, red);
            m.Block("Barn Front", new Vector3(b1.x - 6, 0, b0.z - 0.3f), new Vector3(b1.x, b1.y, b0.z), wood, SurfaceType.Wood, red);
            m.Block("Barn Front", new Vector3(b0.x + 6, 6, b0.z - 0.3f), new Vector3(b1.x - 6, b1.y, b0.z), wood, SurfaceType.Wood, red);
            m.Block("Hay Loft", new Vector3(b0.x, 4.5f, b0.z + 10), new Vector3(b1.x, 4.8f, b1.z), wood, SurfaceType.Wood, new Color(0.5f, 0.38f, 0.22f));
            m.Ramp("Loft Ladder", new Vector3(b0.x + 2, 0, b0.z + 3), 0f, 1.2f, 4.8f, 55f, wood, SurfaceType.Wood, 0.2f);
            for (int s = -1; s <= 1; s += 2)
                m.Box("Barn Roof", new Vector3(25 + s * 5.5f, b1.y + 2.6f, 0), Quaternion.Euler(0, 0, -s * 30f), new Vector3(12.8f, 0.25f, 21f), T(TextureFactory.Metal), SurfaceType.Metal, new Color(0.35f, 0.35f, 0.38f));
            for (int i = 0; i < 8; i++) m.Box("Hay Bale", new Vector3(Random.Range(17f, 33f), 5.4f, Random.Range(1f, 9f)), Quaternion.Euler(0, Random.Range(0, 90f), 0), new Vector3(1.2f, 0.9f, 0.8f), T(TextureFactory.Sand), SurfaceType.Carpet, new Color(0.9f, 0.78f, 0.4f));
            m.Cylinder("Silo", new Vector3(42, 0, 4), 3.2f, 16f, T(TextureFactory.Metal), SurfaceType.Metal, new Color(0.7f, 0.72f, 0.75f), 20);
            m.Building(new Vector3(-30, 0, -40), new Vector3(-15, 7, -28), TextureFactory.Wood, new Color(0.85f, 0.82f, 0.75f), true);
            m.Fence(new Vector3(-65, 0, 5), new Vector3(0, 0, 5)); m.Fence(new Vector3(-65, 0, 5), new Vector3(-65, 0, 52)); m.Fence(new Vector3(0, 0, 5), new Vector3(0, 0, 52));
            m.Car(new Vector3(0, 0, -30), 70f, new Color(0.25f, 0.4f, 0.6f));
            for (int i = 0; i < 25; i++) { var p = new Vector3(Random.Range(-75f, 75f), 0, Random.Range(-75f, 75f)); if (Mathf.Abs(p.x) > 55 || Mathf.Abs(p.z) > 60) m.Tree(p, Random.Range(0.9f, 1.4f), false); }
            m.PlayerSpawn = new Vector3(0, 0.1f, -45f);
            m.NpcSpawns.AddRange(new[] { new Vector3(25, 0.1f, 0), new Vector3(-20, 0.1f, -20), new Vector3(-30, 0.1f, 25), new Vector3(10, 0.1f, 20), new Vector3(40, 0.1f, -15) });
            m.Playable = new Bounds(new Vector3(0, 10, 0), new Vector3(156, 40, 156));
        }

        // ------------------------------------------------------------------ Desert Gas Station

        static void GasStation(MapBuilder m)
        {
            m.Block("Desert", new Vector3(-90, -1, -90), new Vector3(90, 0, 90), T(TextureFactory.Sand), SurfaceType.Dirt, new Color(0.88f, 0.72f, 0.48f), 0.1f);
            m.Block("Road", new Vector3(-90, 0, -40), new Vector3(90, 0.04f, -32), T(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.3f, 0.3f, 0.3f), 0.2f);
            m.Block("Forecourt", new Vector3(-16, 0, -32), new Vector3(16, 0.05f, 4), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.6f, 0.58f, 0.55f), 0.2f);
            m.Box("Canopy", new Vector3(0, 5f, -16), Quaternion.identity, new Vector3(18f, 0.6f, 10f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.9f, 0.88f));
            m.Box("Canopy Stripe", new Vector3(0, 5f, -21.02f), Quaternion.identity, new Vector3(18f, 0.6f, 0.05f), Mats.Glow(new Color(0.95f, 0.3f, 0.15f), 0.6f), SurfaceType.Metal, null, 1f, false);
            for (int i = -1; i <= 1; i += 2) m.Box("Canopy Post", new Vector3(i * 7f, 2.35f, -16), Quaternion.identity, new Vector3(0.4f, 4.7f, 0.4f), Mats.LitMetal, SurfaceType.Metal, new Color(0.8f, 0.8f, 0.8f));
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(-6 + i * 4f, 0, -16);
                m.Box("Pump Island", p + Vector3.up * 0.1f, Quaternion.identity, new Vector3(1.2f, 0.2f, 3f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.7f, 0.7f, 0.7f));
                m.Box("Pump", p + new Vector3(0, 1.0f, 0), Quaternion.identity, new Vector3(0.7f, 1.6f, 0.5f), Mats.LitMetal, SurfaceType.Metal, new Color(0.85f, 0.2f, 0.15f));
                m.Box("Pump Screen", p + new Vector3(0, 1.4f, -0.26f), Quaternion.identity, new Vector3(0.45f, 0.3f, 0.02f), Mats.Glow(new Color(0.4f, 0.9f, 0.5f), 0.6f), SurfaceType.Glass, null, 1f, false);
                PropFactory.Spawn("explosive_barrel", p + new Vector3(0.8f, 0.6f, 1.2f), Quaternion.identity);
            }
            var l = new GameObject("Canopy Light").AddComponent<Light>(); l.transform.SetParent(m.Root, false); l.transform.position = new Vector3(0, 4.5f, -16);
            l.type = LightType.Point; l.range = 16f; l.intensity = 1.8f; l.color = new Color(1f, 0.95f, 0.85f); l.shadows = LightShadows.Soft; m.Lamps.Add(l);
            m.Building(new Vector3(-10, 0, 6), new Vector3(10, 4.5f, 16), TextureFactory.Plaster, new Color(0.92f, 0.85f, 0.7f), true);
            m.Box("Sign Pole", new Vector3(18, 5, -28), Quaternion.identity, new Vector3(0.4f, 10f, 0.4f), Mats.LitMetal, SurfaceType.Metal, new Color(0.5f, 0.5f, 0.5f));
            var sign = m.Box("Sign", new Vector3(18, 10.5f, -28), Quaternion.identity, new Vector3(5f, 2f, 0.3f), Mats.Glow(new Color(0.95f, 0.45f, 0.15f), 0.9f), SurfaceType.Metal, null);
            if (sign != null) sign.AddComponent<SignFlicker>(); // a dying neon tube
            for (int i = 0; i < 20; i++) m.Rock(new Vector3(Random.Range(-80f, 80f), 0, Random.Range(-10f, 80f)) + (Random.value < 0.5f ? new Vector3(0, 0, 0) : new Vector3(0, 0, -110f)), Random.Range(0.6f, 2.2f));
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3(Random.Range(-80f, 80f), 0, Random.Range(20f, 80f)); var g = new Color(0.3f, 0.5f, 0.25f);
                m.Cylinder("Cactus", p, 0.3f, Random.Range(2f, 4f), Mats.Lit, SurfaceType.Wood, g, 8);
                m.Box("Cactus Arm", p + new Vector3(0.5f, 1.6f, 0), Quaternion.identity, new Vector3(0.9f, 0.25f, 0.25f), Mats.Lit, SurfaceType.Wood, g);
            }
            m.Car(new Vector3(-30, 0, -10), 20f, new Color(0.6f, 0.15f, 0.1f));
            m.PlayerSpawn = new Vector3(-40, 0.1f, -36f); m.PlayerYaw = 90f;
            m.NpcSpawns.AddRange(new[] { new Vector3(-4, 0.1f, -12), new Vector3(5, 0.1f, -20), new Vector3(0, 0.1f, 2), new Vector3(15, 0.1f, -5), new Vector3(-15, 0.1f, -25) });
            m.Playable = new Bounds(new Vector3(0, 10, 0), new Vector3(176, 40, 176));
        }

        // ------------------------------------------------------------------ Forest Cabin

        static void Cabin(MapBuilder m)
        {
            m.Block("Ground", new Vector3(-80, -1, -80), new Vector3(80, 0, 80), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.22f, 0.32f, 0.18f), 0.12f);
            m.Block("Lake", new Vector3(20, -0.05f, 20), new Vector3(60, 0.02f, 60), Mats.Glow(new Color(0.04f, 0.07f, 0.1f), 0.1f), SurfaceType.Water, null, 0.1f, false);
            var wood = T(TextureFactory.Wood); Color log = new Color(0.45f, 0.3f, 0.18f);
            // Log cabin: walls of stacked cylinders-looking boxes, door gap at the front.
            Vector3 c = Vector3.zero; float w = 9f, d = 7f, h = 3.2f;
            for (int k = 0; k < 8; k++)
            {
                float y = 0.2f + k * 0.4f; Color t = log * (k % 2 == 0 ? 1f : 0.9f);
                m.Box("Log", c + new Vector3(0, y, d / 2), Quaternion.identity, new Vector3(w + 0.6f, 0.4f, 0.4f), wood, SurfaceType.Wood, t);
                m.Box("Log", c + new Vector3(-w / 2, y, 0), Quaternion.identity, new Vector3(0.4f, 0.4f, d + 0.6f), wood, SurfaceType.Wood, t);
                m.Box("Log", c + new Vector3(w / 2, y, 0), Quaternion.identity, new Vector3(0.4f, 0.4f, d + 0.6f), wood, SurfaceType.Wood, t);
                if (k < 6) { m.Box("Log", c + new Vector3(-w / 4 - 0.5f, y, -d / 2), Quaternion.identity, new Vector3(w / 2 - 0.4f, 0.4f, 0.4f), wood, SurfaceType.Wood, t); m.Box("Log", c + new Vector3(w / 4 + 0.5f, y, -d / 2), Quaternion.identity, new Vector3(w / 2 - 0.4f, 0.4f, 0.4f), wood, SurfaceType.Wood, t); }
                else m.Box("Log", c + new Vector3(0, y, -d / 2), Quaternion.identity, new Vector3(w + 0.6f, 0.4f, 0.4f), wood, SurfaceType.Wood, t);
            }
            m.Box("Floor", c + new Vector3(0, 0.05f, 0), Quaternion.identity, new Vector3(w, 0.1f, d), wood, SurfaceType.Wood, log * 1.2f);
            for (int s = -1; s <= 1; s += 2) m.Box("Roof", c + new Vector3(s * w / 4f, h + 1.2f, 0), Quaternion.Euler(0, 0, -s * 30f), new Vector3(w * 0.62f, 0.2f, d + 1.2f), wood, SurfaceType.Wood, new Color(0.3f, 0.22f, 0.15f));
            var lamp = new GameObject("Cabin Lamp").AddComponent<Light>(); lamp.transform.SetParent(m.Root, false); lamp.transform.position = c + new Vector3(0, 2.6f, 0);
            lamp.type = LightType.Point; lamp.range = 9f; lamp.intensity = 1.5f; lamp.color = new Color(1f, 0.7f, 0.4f); lamp.shadows = LightShadows.Soft; m.Lamps.Add(lamp);
            m.Box("Table", c + new Vector3(1.5f, 0.45f, 1f), Quaternion.identity, new Vector3(1.6f, 0.9f, 1f), wood, SurfaceType.Wood, log);
            m.Box("Bed", c + new Vector3(-3f, 0.3f, 2f), Quaternion.identity, new Vector3(1.4f, 0.5f, 2.2f), Mats.Lit, SurfaceType.Carpet, new Color(0.55f, 0.2f, 0.18f));
            // Woodshed + chopping block.
            m.Box("Woodshed Roof", new Vector3(-12, 2.2f, 4), Quaternion.Euler(10, 0, 0), new Vector3(4f, 0.15f, 3f), wood, SurfaceType.Wood, log);
            for (int i = 0; i < 4; i++) m.Box("Shed Post", new Vector3(-13.8f + (i % 2) * 3.6f, 1f, 2.7f + (i / 2) * 2.6f), Quaternion.identity, new Vector3(0.15f, 2f + (i / 2) * 0.4f, 0.15f), wood, SurfaceType.Wood, log);
            for (int i = 0; i < 12; i++) m.Box("Firewood", new Vector3(-12.8f + (i % 4) * 0.55f, 0.15f + (i / 4) * 0.3f, 5f), Quaternion.Euler(0, 0, 90), new Vector3(0.28f, 0.28f, 1.2f), wood, SurfaceType.Wood, new Color(0.55f, 0.4f, 0.25f));
            m.Cylinder("Chopping Block", new Vector3(-9, 0, 0), 0.4f, 0.6f, wood, SurfaceType.Wood, log, 10);
            // Campfire.
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; m.Rock(new Vector3(Mathf.Sin(a) * 1f, 0, -9 + Mathf.Cos(a) * 1f), 0.25f); }
            var fire = new GameObject("Campfire").AddComponent<Light>(); fire.transform.SetParent(m.Root, false); fire.transform.position = new Vector3(0, 0.8f, -9);
            fire.type = LightType.Point; fire.range = 10f; fire.intensity = 2f; fire.color = new Color(1f, 0.5f, 0.15f); fire.shadows = LightShadows.Soft; m.Lamps.Add(fire);
            FlickerLight.Maybe(fire, null, 1f);
            m.Box("Embers", new Vector3(0, 0.1f, -9), Quaternion.identity, new Vector3(0.9f, 0.15f, 0.9f), Mats.Glow(new Color(1f, 0.35f, 0.05f), 1.2f), SurfaceType.Wood, null, 1f, false);
            for (int i = 0; i < 140; i++)
            {
                var p = new Vector3(Random.Range(-78f, 78f), 0, Random.Range(-78f, 78f));
                if (p.magnitude < 16f || (p.x > 18 && p.x < 62 && p.z > 18 && p.z < 62)) continue;
                m.Tree(p, Random.Range(0.9f, 1.6f), true);
            }
            m.PlayerSpawn = new Vector3(0, 0.1f, -14f);
            m.NpcSpawns.AddRange(new[] { new Vector3(0, 0.1f, 0), new Vector3(-10, 0.1f, -4), new Vector3(15, 0.1f, 10), new Vector3(-20, 0.1f, 20), new Vector3(5, 0.1f, -20) });
            m.Playable = new Bounds(new Vector3(0, 10, 0), new Vector3(156, 40, 156));
        }
    }

    /// <summary>GS2: emissive signs that stutter like an old neon tube (blink off for a moment every few seconds).</summary>
    public sealed class SignFlicker : MonoBehaviour
    {
        Renderer r; float next, offUntil;
        void Start() { r = GetComponent<Renderer>(); next = Time.time + Random.Range(1f, 5f); }
        void Update()
        {
            if (r == null) return;
            if (Time.time >= next) { next = Time.time + Random.Range(1.5f, 7f); offUntil = Time.time + Random.Range(0.05f, 0.5f); }
            bool off = Time.time < offUntil && Mathf.PerlinNoise(Time.time * 35f, 0.3f) > 0.45f;
            if (r.enabled == off) r.enabled = !off;
        }
    }
}
