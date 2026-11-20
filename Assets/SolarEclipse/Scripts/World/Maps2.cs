using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 maps: The Morgue (the intro's morgue, playable), Rooftops (jump between buildings above the city),
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
                Id = "rooftops", Name = "Rooftops", Size = "Medium - high above the city", Glyph = "building", DefaultTime = TimeOfDay.Sunset, Ambience = "amb_wind",
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
    }
}
