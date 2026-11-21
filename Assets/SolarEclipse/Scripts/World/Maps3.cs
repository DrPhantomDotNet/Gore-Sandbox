using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 maps, part 2: Mountain Lodge (cold: breath fog, hypothermia), Military Base, Stadium,
    /// Moon Base (low gravity while you're there, World/Weather2.cs) and Haunted Mansion.
    /// </summary>
    public static class Maps3
    {
        static Material T(Texture2D t) => MapBuilder.Tex(t);

        public static IEnumerable<MapDef> Defs()
        {
            yield return new MapDef { Id = "lodge", Name = "Mountain Lodge", Size = "Medium - snowy outdoor", Glyph = "home", DefaultTime = TimeOfDay.Sunset, Ambience = "amb_wind",
                Description = "A ski lodge high in the mountains: deep snow, pines, a frozen pond and a warm fireplace inside. It's cold out here: bodies cool down fast.",
                Features = new[] { "Freezing cold", "Lodge with fireplace", "Frozen pond", "Pine forest" },
                ThemeTop = new Color(0.7f, 0.78f, 0.92f), ThemeBottom = new Color(0.9f, 0.92f, 0.95f), Accent = new Color(0.6f, 0.85f, 1f), Build = Lodge };
            yield return new MapDef { Id = "military", Name = "Military Base", Size = "Large - outdoor", Glyph = "target", DefaultTime = TimeOfDay.Day, Ambience = "amb_wind",
                Description = "A fenced army base: barracks, a watchtower, a shooting range with targets, sandbag walls and an obstacle course.",
                Features = new[] { "Shooting range", "Watchtower", "Barracks", "Obstacle course" },
                ThemeTop = new Color(0.55f, 0.65f, 0.8f), ThemeBottom = new Color(0.45f, 0.45f, 0.3f), Accent = new Color(0.7f, 0.85f, 0.4f), Build = Military };
            yield return new MapDef { Id = "stadium", Name = "Stadium", Size = "Huge - outdoor", Glyph = "flag", DefaultTime = TimeOfDay.Night, Ambience = "amb_wind",
                Description = "A football stadium under floodlights: a full pitch with goals, stands all around and a tunnel to the changing rooms.",
                Features = new[] { "Full-size pitch", "Floodlights", "Stands", "Goals" },
                ThemeTop = new Color(0.1f, 0.12f, 0.2f), ThemeBottom = new Color(0.25f, 0.5f, 0.25f), Accent = new Color(1f, 1f, 0.85f), Build = Stadium };
            yield return new MapDef { Id = "moonbase", Name = "Moon Base", Size = "Medium - low gravity", Glyph = "moon", DefaultTime = TimeOfDay.Night, Ambience = "amb_hum",
                Description = "A research outpost on the Moon: grey dust, craters, domed modules and the Earth hanging in the sky. Gravity is a sixth of normal.",
                Features = new[] { "Low gravity", "Craters", "Dome modules", "Landing pad" },
                ThemeTop = new Color(0.02f, 0.02f, 0.05f), ThemeBottom = new Color(0.55f, 0.55f, 0.57f), Accent = new Color(0.7f, 0.8f, 1f), Build = Moon };
            yield return new MapDef { Id = "mansion", Name = "Haunted Mansion", Size = "Medium - indoor", Glyph = "ghost", DefaultTime = TimeOfDay.Night, Indoor = true, Ambience = "amb_drone",
                Description = "An abandoned mansion: a grand hall with a staircase, a dusty dining room, portraits that watch you and candles that won't stay lit.",
                Features = new[] { "Grand staircase", "Dining room", "Flickering candles", "Creaking floors" },
                ThemeTop = new Color(0.08f, 0.05f, 0.1f), ThemeBottom = new Color(0.3f, 0.2f, 0.18f), Accent = new Color(0.7f, 0.5f, 1f), Build = Mansion };
        }

        static void Lodge(MapBuilder m)
        {
            m.Block("Snow", new Vector3(-80, -1, -80), new Vector3(80, 0, 80), T(TextureFactory.Sand), SurfaceType.Dirt, new Color(0.93f, 0.95f, 0.98f), 0.1f);
            m.Block("Pond Ice", new Vector3(18, -0.02f, 10), new Vector3(38, 0.03f, 28), Mats.Glass, SurfaceType.Glass, new Color(0.75f, 0.88f, 1f));
            var wood = T(TextureFactory.Wood); var wc = new Color(0.5f, 0.35f, 0.22f);
            m.Block("Floor", new Vector3(-10, -0.2f, -6), new Vector3(10, 0.02f, 6), wood, SurfaceType.Wood, wc * 0.8f);
            m.Block("Roof", new Vector3(-10.5f, 5, -6.5f), new Vector3(10.5f, 5.4f, 6.5f), wood, SurfaceType.Wood, wc * 0.6f);
            m.Block("Wall", new Vector3(-10.3f, 0, -6), new Vector3(-10, 5, 6), wood, SurfaceType.Wood, wc);
            m.Block("Wall", new Vector3(10, 0, -6), new Vector3(10.3f, 5, 6), wood, SurfaceType.Wood, wc);
            m.Block("Wall", new Vector3(-10, 0, 6), new Vector3(10, 5, 6.3f), wood, SurfaceType.Wood, wc);
            m.Block("Wall", new Vector3(-10, 0, -6.3f), new Vector3(-1.3f, 5, -6), wood, SurfaceType.Wood, wc);   // front wall with a doorway
            m.Block("Wall", new Vector3(1.3f, 0, -6.3f), new Vector3(10, 5, -6), wood, SurfaceType.Wood, wc);
            m.Block("Wall", new Vector3(-1.3f, 2.6f, -6.3f), new Vector3(1.3f, 5, -6), wood, SurfaceType.Wood, wc);
            m.Block("Fireplace", new Vector3(-2, 0, 5.3f), new Vector3(2, 3.5f, 6f), T(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.5f, 0.45f, 0.42f));
            var fire = new GameObject("Hearth").AddComponent<Light>(); fire.transform.SetParent(m.Root, false); fire.transform.position = new Vector3(0, 0.8f, 4.6f);
            fire.type = LightType.Point; fire.range = 10f; fire.intensity = 2.2f; fire.color = new Color(1f, 0.55f, 0.2f); m.Lamps.Add(fire); FlickerLight.Maybe(fire, null, 1f);
            m.Box("Embers", new Vector3(0, 0.15f, 5.1f), Quaternion.identity, new Vector3(1.4f, 0.2f, 0.5f), Mats.Glow(new Color(1f, 0.4f, 0.08f), 1.5f), SurfaceType.Wood, null, 1f, false);
            for (int i = 0; i < 3; i++) m.Box("Couch", new Vector3(-4 + i * 4, 0.4f, 1.5f), Quaternion.identity, new Vector3(2.2f, 0.8f, 0.9f), Mats.Lit, SurfaceType.Carpet, new Color(0.55f, 0.15f, 0.12f));
            for (int i = 0; i < 90; i++) { var p = new Vector3(Random.Range(-76f, 76f), 0, Random.Range(-76f, 76f)); if (p.magnitude > 18f && !(p.x > 15 && p.x < 41 && p.z > 7 && p.z < 31)) m.Tree(p, Random.Range(1f, 1.8f), true); }
            m.PlayerSpawn = new Vector3(0, 0.1f, -14f);
            m.NpcSpawns.AddRange(new[] { new Vector3(0, 0.1f, 0), new Vector3(-5, 0.1f, 3), new Vector3(25, 0.1f, 18), new Vector3(-20, 0.1f, -20), new Vector3(10, 0.1f, -25) });
            m.Playable = new Bounds(new Vector3(0, 10, 0), new Vector3(156, 40, 156));
        }

        static void Military(MapBuilder m)
        {
            m.Block("Ground", new Vector3(-70, -1, -70), new Vector3(70, 0, 70), T(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.5f, 0.45f, 0.32f), 0.12f);
            m.Fence(new Vector3(-65, 0, -65), new Vector3(65, 0, -65), 3f); m.Fence(new Vector3(-65, 0, 65), new Vector3(65, 0, 65), 3f);
            m.Fence(new Vector3(-65, 0, -65), new Vector3(-65, 0, 65), 3f); m.Fence(new Vector3(65, 0, -65), new Vector3(65, 0, 65), 3f);
            var olive = new Color(0.35f, 0.4f, 0.25f);
            for (int i = 0; i < 3; i++) m.Building(new Vector3(-50 + i * 18, 0, 30), new Vector3(-38 + i * 18, 5, 50), TextureFactory.Metal, olive, true);
            for (int i = 0; i < 4; i++) m.Box("Tower Leg", new Vector3(30 + (i % 2) * 4, 5, 30 + (i / 2) * 4), Quaternion.identity, new Vector3(0.3f, 10f, 0.3f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.4f, 0.3f, 0.2f));
            m.Block("Tower Deck", new Vector3(29.5f, 10, 29.5f), new Vector3(34.5f, 10.3f, 34.5f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.45f, 0.33f, 0.22f));
            m.Box("Tower Ladder", new Vector3(32, 5, 28.8f), Quaternion.Euler(-10, 0, 0), new Vector3(0.8f, 10.4f, 0.1f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.4f, 0.3f, 0.2f));
            // Shooting range: lanes with targets.
            for (int i = 0; i < 6; i++)
            {
                m.Box("Lane", new Vector3(-30 + i * 4, 0.6f, -20), Quaternion.identity, new Vector3(0.2f, 1.2f, 3f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.5f, 0.4f, 0.28f));
                PropFactory.Spawn("target", new Vector3(-28 + i * 4, 0.1f, -50), Quaternion.identity);
            }
            for (int i = 0; i < 14; i++) { float a = i / 14f * Mathf.PI * 2f; m.Box("Sandbags", new Vector3(Mathf.Sin(a) * 9f, 0.5f, Mathf.Cos(a) * 9f), Quaternion.Euler(0, a * Mathf.Rad2Deg, 0), new Vector3(3.4f, 1f, 0.8f), T(TextureFactory.Sand), SurfaceType.Dirt, new Color(0.7f, 0.62f, 0.45f)); }
            for (int i = 0; i < 6; i++) m.Box("Hurdle", new Vector3(40, 0.5f, -40 + i * 7), Quaternion.identity, new Vector3(4f, 1f + (i % 3) * 0.4f, 0.3f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.5f, 0.4f, 0.28f));
            m.PlayerSpawn = new Vector3(0, 0.1f, -60f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-44, 0.1f, 25), new Vector3(0, 0.1f, 0), new Vector3(32, 10.4f, 32), new Vector3(-20, 0.1f, -25), new Vector3(40, 0.1f, -20) });
            m.Playable = new Bounds(new Vector3(0, 10, 0), new Vector3(136, 40, 136));
        }

        static void Stadium(MapBuilder m)
        {
            m.Block("Ground", new Vector3(-90, -1, -90), new Vector3(90, 0, 90), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.4f, 0.4f, 0.42f), 0.1f);
            m.Block("Pitch", new Vector3(-34, 0, -52), new Vector3(34, 0.04f, 52), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.3f, 0.58f, 0.28f), 0.15f);
            m.Box("Halfway Line", new Vector3(0, 0.05f, 0), Quaternion.identity, new Vector3(68f, 0.01f, 0.15f), Mats.Lit, SurfaceType.Grass, Color.white, 1f, false);
            for (int s = -1; s <= 1; s += 2) PropFactory.Spawn("goal", new Vector3(0, 0.05f, s * 50f), Quaternion.Euler(0, s > 0 ? 180f : 0f, 0));
            PropFactory.Spawn("ball", new Vector3(0, 0.5f, 0), Quaternion.identity);
            var stand = new Color(0.55f, 0.15f, 0.15f);
            for (int t = 0; t < 8; t++)
            {
                float h = 1f + t * 1.2f, off = 4f + t * 1.6f;
                m.Block("Stand", new Vector3(-36 - off, 0, -56), new Vector3(-34.5f - off, h, 56), T(TextureFactory.Concrete), SurfaceType.Concrete, stand * (0.8f + t * 0.03f));
                m.Block("Stand", new Vector3(34.5f + off, 0, -56), new Vector3(36 + off, h, 56), T(TextureFactory.Concrete), SurfaceType.Concrete, stand * (0.8f + t * 0.03f));
                m.Block("Stand", new Vector3(-36, 0, 54.5f + off), new Vector3(36, h, 56 + off), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.15f, 0.2f, 0.5f));
                m.Block("Stand", new Vector3(-36, 0, -56 - off), new Vector3(36, h, -54.5f - off), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.15f, 0.2f, 0.5f));
            }
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i < 2 ? -55 : 55, 0, i % 2 == 0 ? -70 : 70);
                m.Box("Floodlight Mast", p + Vector3.up * 15, Quaternion.identity, new Vector3(0.8f, 30f, 0.8f), Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.6f, 0.62f));
                m.Box("Floodlight", p + Vector3.up * 30.5f, Quaternion.LookRotation(-p), new Vector3(5f, 3f, 0.5f), Mats.Glow(new Color(1f, 1f, 0.92f), 2f), SurfaceType.Metal, null);
                var l = new GameObject("Flood").AddComponent<Light>(); l.transform.SetParent(m.Root, false); l.transform.position = p + Vector3.up * 30f; l.transform.rotation = Quaternion.LookRotation(-p + Vector3.down * 30f);
                l.type = LightType.Spot; l.spotAngle = 70f; l.range = 140f; l.intensity = 2.5f; l.color = new Color(1f, 1f, 0.92f); l.shadows = i == 0 ? LightShadows.Soft : LightShadows.None; m.Lamps.Add(l);
            }
            m.PlayerSpawn = new Vector3(0, 0.1f, -30f);
            m.NpcSpawns.AddRange(new[] { new Vector3(0, 0.1f, 10), new Vector3(-15, 0.1f, 25), new Vector3(15, 0.1f, -10), new Vector3(-10, 0.1f, -40), new Vector3(20, 0.1f, 40) });
            m.Playable = new Bounds(new Vector3(0, 10, 0), new Vector3(176, 50, 176));
        }

        static void Moon(MapBuilder m)
        {
            m.Block("Regolith", new Vector3(-90, -1, -90), new Vector3(90, 0, 90), T(TextureFactory.Sand), SurfaceType.Dirt, new Color(0.52f, 0.52f, 0.54f), 0.08f);
            for (int i = 0; i < 14; i++)
            {
                var c = new Vector3(Random.Range(-70f, 70f), 0, Random.Range(-70f, 70f)); if (c.magnitude < 20f) continue;
                float r = Random.Range(3f, 9f);
                for (int k = 0; k < 16; k++) { float a = k * Mathf.PI / 8f; m.Rock(c + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r, Random.Range(0.6f, 1.4f)); }
            }
            var white = new Color(0.88f, 0.89f, 0.92f);
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i * 12f - 18f, 0, 12f);
                m.Cylinder("Module", p, 4.5f, 3.5f, Mats.LitMetal, SurfaceType.Metal, white, 20);
                m.Box("Window", p + new Vector3(0, 2.2f, -4.45f), Quaternion.identity, new Vector3(2f, 0.6f, 0.05f), Mats.Glow(new Color(0.6f, 0.85f, 1f), 0.8f), SurfaceType.Glass, null, 1f, false);
                if (i < 3) m.Box("Tunnel", p + new Vector3(6f, 1.2f, 0), Quaternion.identity, new Vector3(3f, 2.4f, 2.2f), Mats.LitMetal, SurfaceType.Metal, white * 0.9f);
            }
            m.Cylinder("Landing Pad", new Vector3(0, 0, -30), 9f, 0.15f, Mats.Lit, SurfaceType.Concrete, new Color(0.3f, 0.3f, 0.32f), 28);
            m.Box("Earth", new Vector3(120, 160, 300), Quaternion.identity, new Vector3(60, 60, 1), Mats.Glow(new Color(0.25f, 0.5f, 0.95f), 1.2f), SurfaceType.Glass, null, 1f, false);
            m.PlayerSpawn = new Vector3(0, 0.3f, -30f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-18, 0.1f, 4), new Vector3(6, 0.1f, 4), new Vector3(20, 0.1f, -10), new Vector3(-25, 0.1f, -25), new Vector3(10, 0.1f, 30) });
            m.Playable = new Bounds(new Vector3(0, 30, 0), new Vector3(176, 80, 176));
        }

        static void Mansion(MapBuilder m)
        {
            var wall = new Color(0.32f, 0.2f, 0.22f);
            Maps2.Room(m, new Vector3(-14, 0, -12), new Vector3(14, 8, 12), TextureFactory.Wallpaper, wall, SurfaceType.Wood);
            m.Block("Floorboards", new Vector3(-14, -0.02f, -12), new Vector3(14, 0.01f, 12), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.35f, 0.24f, 0.16f));
            m.Block("Gallery", new Vector3(-14, 4, 6), new Vector3(14, 4.3f, 12), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.3f, 0.2f, 0.14f));
            m.Stairs("Grand Staircase", new Vector3(0, 0, -2), 0f, 4f, 13, 0.31f, 0.6f, T(TextureFactory.Carpet), SurfaceType.Carpet, new Color(0.5f, 0.08f, 0.1f));
            m.Box("Dining Table", new Vector3(-8, 0.4f, -6), Quaternion.identity, new Vector3(2f, 0.8f, 6f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.25f, 0.15f, 0.1f));
            for (int i = 0; i < 6; i++) m.Box("Chair", new Vector3(-9.6f + (i % 2) * 3.2f, 0.5f, -8f + (i / 2) * 2f), Quaternion.identity, new Vector3(0.5f, 1f, 0.5f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.3f, 0.18f, 0.12f));
            for (int i = 0; i < 6; i++) m.Box("Portrait", new Vector3(-13.9f, 2.6f, -9 + i * 3.4f), Quaternion.Euler(0, 90, 0), new Vector3(1.2f, 1.6f, 0.08f), Mats.Lit, SurfaceType.Wood, new Color(0.15f, 0.12f, 0.1f) + new Color(Random.value * 0.2f, Random.value * 0.1f, 0f), 1f, false);
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(Random.Range(-12f, 12f), 1.1f, Random.Range(-10f, 10f));
                var c = new GameObject("Candle").AddComponent<Light>(); c.transform.SetParent(m.Root, false); c.transform.position = p;
                c.type = LightType.Point; c.range = 5f; c.intensity = 1.2f; c.color = new Color(1f, 0.65f, 0.3f); c.shadows = LightShadows.None; m.Lamps.Add(c);
                FlickerLight.Maybe(c, null, 1f);
                m.Box("Candle Stick", p - Vector3.up * 0.15f, Quaternion.identity, new Vector3(0.05f, 0.25f, 0.05f), Mats.Glow(new Color(1f, 0.9f, 0.7f), 0.8f), SurfaceType.Wood, null, 1f, false);
            }
            m.PlayerSpawn = new Vector3(0, 0.1f, -10.5f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-8, 0.1f, -2), new Vector3(8, 0.1f, -5), new Vector3(0, 4.4f, 9), new Vector3(10, 0.1f, 8), new Vector3(-10, 4.4f, 9) });
            m.Playable = new Bounds(new Vector3(0, 4, 0), new Vector3(28, 8, 24));
        }
    }
}
